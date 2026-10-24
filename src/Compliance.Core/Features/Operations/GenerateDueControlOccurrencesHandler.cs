using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Selects the due recurring occurrences of one active Type II period for a generation key. Each selected
///     occurrence freezes its complete creation intent from the approved operating plan in force for its window; the
///     whole selection is one immutable manifest with no count cap. The generation key is the logical identity, so an
///     exact retry replays the original selection. Materializing the selected occurrences is the fan-out's work.
/// </summary>
public sealed class GenerateDueControlOccurrencesHandler(NativeOperationExecutor operations, IAggregateReader reader,
    ControlOperationsReader operatingReader, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<GenerateDueControlOccurrences>
{
    const string CommandKind = "GenerateDueControlOccurrences";

    public async ValueTask<Result> HandleAsync(IRequestContext<GenerateDueControlOccurrences> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Recurring occurrence generation requires an authorized personal HTTP actor."));

        var now = clock.GetUtcNow();
        var key = request.GenerationKey?.Trim() ?? string.Empty;
        var logicalId = TypeIIPeriod.GenerationLogicalId(request.PeriodId, key);
        var stream = new TypeIIPeriod(request.TenantId, request.ProgramId, request.PeriodId).Stream;
        var provenance = new OperationProvenance(logicalId, logicalId, null, CommandKind,
            ActorReference.ForMember(actorMemberId, actorDisplay), stream, ControlOperationsSource.InitialEpisode, null,
            AuthoredIntentDigest.Compute(request), now, stream, stream);
        var binding = AcceptedOperationBinding.Of(provenance);
        var period = await reader.HydrateAsync(new TypeIIPeriod(request.TenantId, request.ProgramId,
            request.PeriodId, logicalId), ct).ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? ids = null;
        ImmutableContentRef? intents = null;
        ImmutableContentRef? result = null;
        var generations = period.OccurrenceGenerations.Count;
        if (period.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (period.PrepareGenerateDueOccurrences(key, request.ThroughDate, actorMemberId, actorDisplay, now) is
                { } failure)
                return Result.Failure(failure.ToRequestError());
            var selected = await SelectAsync(period, request, provenance, now, ct).ConfigureAwait(false);
            if (!selected.IsSuccess)
                return Result.Failure(selected.Error!);
            var selections = selected.Value!;
            ids = await ControlOperatingContent.WriteAsync(content, request.TenantId,
                ControlOperatingContent.OccurrenceIdsSchema,
                (IReadOnlyList<Uuid>)selections.Select(static selection => selection.OccurrenceId).ToArray(), ct)
                .ConfigureAwait(false);
            intents = await ControlOperatingContent.WriteAsync(content, request.TenantId,
                ControlOccurrenceSelectionManifest.Schema, new ControlOccurrenceSelectionManifest(selections), ct)
                .ConfigureAwait(false);
            var unit = await ControlOperatingContent.UnitResponseAsync(content, request.TenantId, ct)
                .ConfigureAwait(false);
            response = unit;
            result = await ControlOperatingContent.WriteResultAsync(content, request.TenantId, UnitResult.Schema,
                ((StandardResponseBody)unit.Body).Content, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<TypeIIPeriod>(binding,
            () => new TypeIIPeriod(request.TenantId, request.ProgramId, request.PeriodId, logicalId),
            owner => owner.Acceptance, owner => Prepare(owner, generations, response),
            (TypeIIPeriod owner, ImmutableContentRef frozen) => owner.GenerateDueOccurrences(provenance, key,
                request.ThroughDate, actorMemberId, actorDisplay, now, ids!, intents!, frozen, result!)),
            context, ct).ConfigureAwait(false);
        if (!outcome.IsSuccess)
            return Result.Failure(outcome.Error!);
        return outcome.Value is AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed
            ? Result.Success
            : Result.Failure(AcceptedAppendResult.ConflictError);
    }

    static Result<FirstResponseDescriptor> Prepare(TypeIIPeriod period, int generations,
        FirstResponseDescriptor? response) =>
        period.OccurrenceGenerations.Count != generations || response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The period's occurrence generations changed while the selection was prepared; retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);

    async ValueTask<Result<IReadOnlyList<ControlOccurrenceSelection>>> SelectAsync(TypeIIPeriod period,
        GenerateDueControlOccurrences request, OperationProvenance provenance, DateTimeOffset now, CancellationToken ct)
    {
        var current = period.Current!;
        var already = new HashSet<Uuid>();
        foreach (var generation in period.OccurrenceGenerations)
        {
            var prior = await ControlOperatingContent.ReadAsync<ControlOccurrenceSelectionManifest>(content,
                request.TenantId, generation.CreationIntentManifest, ct).ConfigureAwait(false);
            foreach (var selection in prior.Selections)
                already.Add(selection.ExpectedOccurrenceId);
        }

        var header = new DurableIntentHeader(provenance.RootOperationId, provenance.Owner, provenance.AffectedOwner,
            provenance.ExpectedRevisionOwner, provenance.Actor, provenance.Episode, provenance.OriginalExpectedRevision,
            provenance.AuthoredIntentSha256, now);
        var selections = new SortedDictionary<string, ControlOccurrenceSelection>(StringComparer.Ordinal);
        var lines = new Dictionary<Uuid, ControlOperatingPlanLine>();
        foreach (var item in period.DueExpectedOccurrences(request.ThroughDate, already))
        {
            if (!lines.TryGetValue(item.ControlId, out var line))
                lines[item.ControlId] = line = (await operatingReader.ReadLineAsync(request.TenantId,
                    request.ProgramId, item.ControlId, Uuid.Empty, ct).ConfigureAwait(false)).Line;
            var window = WindowOf(line, current, item);
            if (window is null || line.PlanOn(window.From) is not { } plan ||
                !line.Approvals.TryGetValue(plan.PlanVersionId, out var approval))
                return Result<IReadOnlyList<ControlOccurrenceSelection>>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "A due recurring control has no approved operating plan period for its occurrence; the selection fails closed."));
            var occurrenceId = ControlOccurrenceIdentity.Of(request.TenantId, request.ProgramId, item.ControlId, window);
            var intent = new ControlOccurrenceCreationIntent(header, request.TenantId, request.ProgramId,
                item.ControlId, occurrenceId, window, plan.PlanVersionId,
                NativeFactReference.Of(new ControlOperatingPlan(request.TenantId, request.ProgramId,
                    item.ControlId).Stream, approval), plan.Owner, plan.BackupOwner, plan.ReviewerMemberId,
                ControlOccurrenceCreationIntent.RecurringTrigger, now);
            var reference = await ControlOperatingContent.WriteAsync(content, request.TenantId,
                ControlOccurrenceCreationIntent.Schema, intent, ct).ConfigureAwait(false);
            if (!selections.TryAdd(occurrenceId.ToString(), new ControlOccurrenceSelection(item.OccurrenceId,
                    occurrenceId, reference)))
                return Result<IReadOnlyList<ControlOccurrenceSelection>>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "Two expected occurrences select one business window."));
        }

        return Result<IReadOnlyList<ControlOccurrenceSelection>>.Success(selections.Values.ToArray());
    }

    /// <summary>The calendar window of an expected recurring occurrence, found by its due date in the plan's cadence.</summary>
    static ControlCalendarWindow? WindowOf(ControlOperatingPlanLine line, TypeIIPeriodView period,
        TypeIIPeriodOccurrenceView item)
    {
        if (line.PlanOn(period.PeriodStart) is not { } plan || item.DueOn is null)
            return null;
        var matches = ControlCadenceSchedule.Periods(plan.Cadence, period.PeriodStart, null, period.PeriodEnd)
            .Where(candidate => candidate.DueOn == item.DueOn).ToArray();
        return matches.Length == 1 ? ControlOccurrenceIdentity.WindowOf(matches[0]) : null;
    }
}

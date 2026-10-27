using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>
///     Selects the due recurring occurrences of one active Type II period for a generation key. Each selected
///     occurrence freezes its complete creation intent from the approved operating plan in force for its window; the
///     whole selection is one immutable manifest with no count cap. The generation key is the logical identity, so an
///     exact retry replays the original selection. Materializing the selected occurrences is the fan-out's work.
/// </summary>
public sealed class GenerateDueControlOccurrencesHandler(TypeIIPeriodOperations operations,
    ControlOperationsReader operatingReader, IImmutableContentStore content)
    : IRequestHandler<GenerateDueControlOccurrences, UnitResult>
{
    public async ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<GenerateDueControlOccurrences> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId, out var member, out var display))
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Forbidden, "Recurring occurrence generation requires a personal HTTP actor."));
        var key = request.GenerationKey?.Trim() ?? string.Empty;
        var logicalId = TypeIIPeriod.GenerationLogicalId(request.PeriodId, key);
        return await operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.PeriodId, logicalId,
            nameof(GenerateDueControlOccurrences), null, async (snapshot, operation, token) =>
            {
                if (snapshot.Rules.PrepareGenerateDueOccurrences(key, request.ThroughDate, member, display, operation.AcceptedAt) is { } invalid)
                    return Result.Failure(invalid.ToRequestError());
                var selected = await SelectAsync(snapshot, request, operation, operation.AcceptedAt, token).ConfigureAwait(false);
                if (!selected.IsSuccess)
                    return Result.Failure(selected.Error!);
                var ids = await ControlOperatingContent.WriteAsync(content, request.TenantId, ControlOperatingContent.OccurrenceIdsSchema,
                    (IReadOnlyList<Uuid>)selected.Value!.Select(selection => selection.OccurrenceId).ToArray(), token).ConfigureAwait(false);
                var intents = await ControlOperatingContent.WriteAsync(content, request.TenantId, ControlOccurrenceSelectionManifest.Schema,
                    new ControlOccurrenceSelectionManifest(selected.Value!), token).ConfigureAwait(false);
                return snapshot.Rules.GenerateDueOccurrences(operation, key, request.ThroughDate, member, display, operation.AcceptedAt,
                    ids, intents, null, null);
            }, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<IReadOnlyList<ControlOccurrenceSelection>>> SelectAsync(TypeIIPeriodReadSnapshot period,
        GenerateDueControlOccurrences request, OperationProvenance provenance, DateTimeOffset now, CancellationToken ct)
    {
        var current = period.View!;
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
        foreach (var item in period.Rules.DueExpectedOccurrences(request.ThroughDate, already))
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

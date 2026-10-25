using Bdgrz.Compliance.Features.SystemDescription;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.TypeIIPlan;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Admits a period opening from an approved plan and its exact frozen Type I source chain. The complete opening,
///     the exact range and the first 202 acknowledgment are frozen on the period owner before the calendar is touched;
///     <c>TypeIIRangeProcess</c> then acquires the range and applies the opening. An exact retry replays the original
///     acknowledgment even after the plan or the period changed.
/// </summary>
public sealed class StartTypeIIObservationPeriodHandler(NativeOperationExecutor operations, IAggregateReader reader, ControlReader controlSources,
    FindingReader findings, ControlOperationsReader operating, IImmutableContentStore content, TimeProvider clock, SystemDescriptionReader descriptions)
    : IRequestHandler<StartTypeIIObservationPeriod, OperationAcceptedResponse>
{
    const string CommandKind = "StartTypeIIObservationPeriod";
    const string InitialEpisode = "initial";

    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<StartTypeIIObservationPeriod> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<OperationAcceptedResponse>> HandleCoreAsync(
        IRequestContext<StartTypeIIObservationPeriod> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Failure(RequestErrorKind.Forbidden, "Starting a Type II period requires a personal HTTP actor.");
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.PeriodId == Uuid.Empty || request.ExaminationId == Uuid.Empty ||
            request.PlanId == Uuid.Empty || request.PeriodStart == default ||
            request.PeriodEnd < request.PeriodStart)
            return Failure(RequestErrorKind.Validation,
                "A Type II period requires valid identities and a nonempty date range.");

        var realm = request.TenantId.ToString();
        var now = clock.GetUtcNow();
        var snapshot = new TypeIIPeriod(request.TenantId, request.ProgramId, request.PeriodId);
        var provenance = new OperationProvenance(request.PeriodId, request.PeriodId, null, CommandKind,
            ActorReference.ForMember(actorMemberId, actorDisplay), snapshot.Stream, InitialEpisode, null,
            AuthoredIntentDigest.Compute(request), now, snapshot.Stream, snapshot.Stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        var sequence = 0L;
        var position = 0UL;
        var outcome = await operations.ExecuteAsync(new NativeOperation<TypeIIPeriod>(binding,
            () => new TypeIIPeriod(request.TenantId, request.ProgramId, request.PeriodId, request.PeriodId),
            period => period.Acceptance,
            async (period, token) =>
            {
                try
                {
                    var guard = period.PrepareOpeningAcceptance();
                    if (!guard.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(guard.Error!);
                    var rules = new TypeIIPeriodRules(request.TenantId, request.ProgramId, request.PeriodId);
                    var frozen = await FreezeAsync(request, rules, provenance, actorMemberId, actorDisplay, now, token).ConfigureAwait(false);
                    if (!frozen.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(frozen.Error!);
                    intent = frozen.Value.Intent;
                    sequence = period.NativeSequence;
                    position = period.CommittedStreamPosition;
                    return Result<FirstResponseDescriptor>.Success(frozen.Value.Response);
                }
                catch (ImmutableContentIntegrityException exception)
                {
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The period's exact immutable preparation cannot be verified.", exception is ImmutableContentUnavailableException));
                }
            }, (TypeIIPeriod period, ImmutableContentRef acknowledgment) =>
                period.AtCut(sequence, position) is { IsSuccess: false } changed ? changed :
                period.AcceptOpening(provenance, intent!, acknowledgment)), context, ct).ConfigureAwait(false);
        return await TypeIIRangeContent.AcknowledgmentAsync(content, realm, outcome, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<(ImmutableContentRef Intent, FirstResponseDescriptor Response)>> FreezeAsync(
        StartTypeIIObservationPeriod request, TypeIIPeriodRules period, OperationProvenance provenance, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset now, CancellationToken ct)
    {
        var plan = await reader.HydrateAsync(new TypeIIPlanAggregate(request.TenantId,
            request.ProgramId, request.PlanId), ct).ConfigureAwait(false);
        if (plan.Current is not { } planView)
            return Fail(RequestErrorKind.NotFound, "The Type II operating plan was not found.");
        var planVersion = planView.Current;
        if (!Approved(planView) || planView.TypeIIPlanId != request.PlanId ||
            planView.TenantId != request.TenantId || planView.ProgramId != request.ProgramId ||
            planVersion is null || planVersion.Content.ExaminationId != request.ExaminationId ||
            planVersion.Content.PeriodStart != request.PeriodStart ||
            planVersion.Content.PeriodEnd != request.PeriodEnd)
            return Fail(RequestErrorKind.Conflict, "The period must match the exact current approved Type II plan.");

        var planContent = planVersion.Content;
        var checkedSources = await TypeIIPlanSourceValidator.ValidateAsync(reader, controlSources, findings, operating, descriptions,
            request.TenantId, request.ProgramId, request.ExaminationId, planContent, now, ct)
            .ConfigureAwait(false);
        if (!checkedSources.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(checkedSources.Error!);
        var sources = checkedSources.Value;
        var baseline = sources.Baseline;
        var outcomeView = sources.Outcome;
        var descriptionVersion = sources.SystemDescription;
        var approval = sources.Approval;
        var binding = sources.Binding;
        var planApproval = planView.Approvals.Single(item => item.PlanVersion == planView.CurrentVersionNumber);
        if (now == default || now < planApproval.ApprovedAt ||
            planVersion.ProposedAt < baseline.UpdatedAt ||
            planVersion.ProposedAt < outcomeView.RecordedAt ||
            planVersion.ProposedAt < descriptionVersion.UpdatedAt ||
            planVersion.ProposedAt < approval.ApprovedAt ||
            planVersion.ProposedAt < binding.BoundAt)
            return Fail(RequestErrorKind.Conflict,
                "The period cannot start before its approved plan and frozen source records exist.");

        var sourceReferences = baseline.Sources.Select(source => new ExaminationContentReference(
            source.Kind, source.RecordId, source.VersionId, source.Revision, source.ContentSha256))
            .OrderBy(source => source.SourceType, StringComparer.Ordinal)
            .ThenBy(source => source.RecordId.ToString(), StringComparer.Ordinal).ToArray();
        var acknowledgedGaps = planContent.AcknowledgedGaps.Select(item => item.GapId)
            .OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray();
        var openingManifest = new OpeningManifest(request.TenantId, request.ProgramId,
            request.ExaminationId, request.PeriodId, request.PlanId, planView.CurrentVersionNumber,
            planVersion.ContentSha256, baseline.TypeIBaselineId, baseline.ContentSha256,
            planContent.TypeIOutcomeId, outcomeView.ContentSha256,
            planContent.SystemDescriptionVersionId, descriptionVersion.ContentSha256,
            sourceReferences, acknowledgedGaps) with
        { SourceContentManifest = baseline.SourceContentManifest };
        var opening = new TypeIIPeriodOpeningContent(request.ExaminationId, request.PeriodId,
            Digest(openingManifest), sourceReferences, acknowledgedGaps)
        { SourceContentManifest = baseline.SourceContentManifest };

        var failure = period.PrepareOpening(planView, opening, actorMemberId, actorDisplay, now, out var initial);
        if (failure is not null)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(failure.ToRequestError());

        var realm = request.TenantId.ToString();
        var change = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), TypeIIPeriodOpeningChange.Schema,
            new TypeIIPeriodOpeningChange(initial!), ct).ConfigureAwait(false);
        var calendar = new TypeIIObservationCalendar(request.TenantId, request.ProgramId).Stream;
        var header = new DurableIntentHeader(request.PeriodId, period.Stream, period.Stream, period.Stream,
            provenance.Actor, InitialEpisode, null, provenance.AuthoredIntentSha256, now);
        var intent = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), TypeIIRangeIntent.Schema,
            new TypeIIRangeIntent(header, request.ProgramId, request.PeriodId, TypeIIRangeOperationKind.Opening, null,
                null, new ObservationRange(initial!.PeriodStart, initial.PeriodEnd), change,
                TypeIIRangeEffects.KeyOf(request.PeriodId, period.Stream, calendar)), ct).ConfigureAwait(false);
        var accepted = await TypeIIRangeContent.AcceptedResponseAsync(content, realm, request.PeriodId, now, ct)
            .ConfigureAwait(false);
        return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Success((intent, accepted));
    }

    static bool Approved(TypeIIPlanView plan)
    {
        if (plan.Status != "approved" || plan.Current is not { } current ||
            current.Version != plan.CurrentVersionNumber || current.Content is null ||
            !HashEquals(current.ContentSha256, Digest(current.Content)))
            return false;
        var approval = plan.Approvals.SingleOrDefault(item => item.PlanVersion == current.Version);
        var review = plan.Reviews.SingleOrDefault(item => item.PlanVersion == current.Version);
        return approval is not null && review?.Outcome == "approve" &&
            approval.ApprovedByMemberId != current.ProposedByMemberId &&
            approval.ApprovedByMemberId != review.ReviewerMemberId &&
            review.ReviewerMemberId != current.ProposedByMemberId &&
            approval.ApproverRole is "ceo" or "ciso";
    }

    static string Digest<T>(T value) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();

    static bool HashEquals(string? left, string? right) => left is not null && right is not null &&
        StringComparer.OrdinalIgnoreCase.Equals(left, right);

    static Result<(ImmutableContentRef, FirstResponseDescriptor)> Fail(RequestErrorKind kind, string message) =>
        Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(new RequestError(kind, message));

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));

    sealed record OpeningManifest(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId,
        Uuid OpeningSnapshotId, Uuid TypeIIPlanId, int TypeIIPlanVersion,
        string TypeIIPlanSha256, Uuid TypeIBaselineId, string TypeIBaselineSha256,
        Uuid TypeIOutcomeId, string TypeIOutcomeSha256, Uuid SystemDescriptionVersionId,
        string SystemDescriptionSha256, IReadOnlyList<ExaminationContentReference> Sources,
        IReadOnlyList<Uuid> AcknowledgedGapIds)
    {
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableContentRef? SourceContentManifest { get; init; }
    }
}

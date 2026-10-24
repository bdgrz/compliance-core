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
public sealed class StartTypeIIObservationPeriodHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IDomainEventReader events, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<StartTypeIIObservationPeriod, OperationAcceptedResponse>
{
    const string CommandKind = "StartTypeIIObservationPeriod";
    const string InitialEpisode = "initial";

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
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
        var snapshot = await reader.HydrateAsync(new TypeIIPeriod(request.TenantId, request.ProgramId,
            request.PeriodId, request.PeriodId), ct).ConfigureAwait(false);
        var provenance = new OperationProvenance(request.PeriodId, request.PeriodId, null, CommandKind,
            ActorReference.ForMember(actorMemberId, actorDisplay), snapshot.Stream, InitialEpisode, null,
            AuthoredIntentDigest.Compute(request), now, snapshot.Stream, snapshot.Stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        // A logical id that was already accepted resolves from the owner's facts before any guard is evaluated.
        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var frozen = await FreezeAsync(request, snapshot, provenance, actorMemberId, actorDisplay, now, ct)
                .ConfigureAwait(false);
            if (!frozen.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(frozen.Error!);
            (intent, response) = frozen.Value;
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<TypeIIPeriod>(binding,
            () => new TypeIIPeriod(request.TenantId, request.ProgramId, request.PeriodId, request.PeriodId),
            period => period.Acceptance,
            period => Prepare(period, response),
            (TypeIIPeriod period, ImmutableContentRef acknowledgment) =>
                period.AcceptOpening(provenance, intent!, acknowledgment)), context, ct).ConfigureAwait(false);
        return await TypeIIRangeContent.AcknowledgmentAsync(content, realm, outcome, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<(ImmutableContentRef Intent, FirstResponseDescriptor Response)>> FreezeAsync(
        StartTypeIIObservationPeriod request, TypeIIPeriod period, OperationProvenance provenance, Uuid actorMemberId,
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
        var checkedSources = await TypeIIPlanSourceValidator.ValidateAsync(reader, events,
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
            sourceReferences, acknowledgedGaps);
        var opening = new TypeIIPeriodOpeningContent(request.ExaminationId, request.PeriodId,
            Digest(openingManifest), sourceReferences, acknowledgedGaps);

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

    static Result<FirstResponseDescriptor> Prepare(TypeIIPeriod period, FirstResponseDescriptor? response)
    {
        var prepared = period.PrepareOpeningAcceptance();
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The period changed while the opening was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
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
        IReadOnlyList<Uuid> AcknowledgedGapIds);
}

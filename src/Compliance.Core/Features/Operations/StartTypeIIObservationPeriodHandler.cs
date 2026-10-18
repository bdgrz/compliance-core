using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using AuditOutcomeAggregate = Bdgrz.Compliance.Features.Examinations.AuditOutcome;
using ExaminationEngagementAggregate = Bdgrz.Compliance.Features.Examinations.ExaminationEngagement;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.SystemDescription;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.TypeIIPlan;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Starts a period from an approved plan and its exact frozen Type I source chain.</summary>
public sealed class StartTypeIIObservationPeriodHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<StartTypeIIObservationPeriod>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<StartTypeIIObservationPeriod> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Failure(RequestErrorKind.Forbidden,
                "Starting a Type II period requires a personal HTTP actor.");
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.PeriodId == Uuid.Empty || request.ExaminationId == Uuid.Empty ||
            request.PlanId == Uuid.Empty || request.PeriodStart == default ||
            request.PeriodEnd < request.PeriodStart)
            return Failure(RequestErrorKind.Validation,
                "A Type II period requires valid identities and a nonempty date range.");

        var plan = await reader.HydrateAsync(new TypeIIPlanAggregate(request.TenantId,
            request.ProgramId, request.PlanId), ct).ConfigureAwait(false);
        if (plan.Current is not { } planView)
            return Failure(RequestErrorKind.NotFound, "The Type II operating plan was not found.");
        var planVersion = planView.Current;
        if (!Approved(planView) || planView.TypeIIPlanId != request.PlanId ||
            planView.TenantId != request.TenantId || planView.ProgramId != request.ProgramId ||
            planVersion is null || planVersion.Content.ExaminationId != request.ExaminationId ||
            planVersion.Content.PeriodStart != request.PeriodStart ||
            planVersion.Content.PeriodEnd != request.PeriodEnd)
            return Failure(RequestErrorKind.Conflict,
                "The period must match the exact current approved Type II plan.");

        var content = planVersion.Content;
        var engagement = await reader.HydrateAsync(new ExaminationEngagementAggregate(request.TenantId,
            request.ProgramId, request.ExaminationId), ct).ConfigureAwait(false);
        if (engagement.Current is null || engagement.Baseline is not { } baseline)
            return Failure(RequestErrorKind.NotFound,
                "The Type I examination and its current baseline were not found.");
        if (engagement.Current.ExaminationEngagementId != request.ExaminationId ||
            baseline.ExaminationEngagementId != request.ExaminationId ||
            baseline.Status is not ("frozen" or "amended") ||
            !ValidBaseline(baseline) || content.TypeIBaselineId != baseline.TypeIBaselineId ||
            content.SourceSnapshotId != baseline.TypeIBaselineId ||
            !HashEquals(content.SourceSnapshotSha256, baseline.ContentSha256))
            return Failure(RequestErrorKind.Conflict,
                "The approved plan must bind the current exact Type I baseline manifest.");

        if (content.CarryForwardInputs.Any(input => !baseline.Sources.Any(source =>
                source.Kind == input.SourceType && source.RecordId == input.RecordId &&
                source.VersionId == input.VersionId && source.Revision == input.Version &&
                HashEquals(source.ContentSha256, input.ContentSha256))) ||
            content.CarriedForwardGaps.Any(gap => !baseline.Sources.Any(source =>
                source.Kind == gap.SourceKind && source.RecordId == gap.SourceRecordId &&
                source.VersionId == gap.SourceVersionId && source.Revision == gap.SourceVersion &&
                HashEquals(source.ContentSha256, gap.SourceSha256))) ||
            content.Controls.Any(control => !baseline.Sources.Any(source =>
                source.Kind == "approved_control" && source.RecordId == control.ControlId &&
                source.VersionId == control.ControlVersionId)))
            return Failure(RequestErrorKind.Conflict,
                "Every carried-forward input, gap, and planned control must belong to the exact frozen baseline.");

        var outcome = await reader.HydrateAsync(new AuditOutcomeAggregate(request.TenantId,
            request.ProgramId, content.TypeIOutcomeId), ct).ConfigureAwait(false);
        if (outcome.Current is not { Status: "recorded" } outcomeView ||
            outcomeView.Content.ExaminationId != request.ExaminationId ||
            outcomeView.Content.ScopeKind != "type_i" ||
            outcomeView.Content.SourceSnapshotId != baseline.TypeIBaselineId ||
            !HashEquals(outcomeView.Content.SourceSnapshotSha256, baseline.ContentSha256))
            return Failure(RequestErrorKind.Conflict,
                "The approved plan must reference an auditor outcome for this exact Type I baseline.");

        var descriptionSources = baseline.Sources.Where(source =>
            source.Kind == "system_description" &&
            source.VersionId == content.SystemDescriptionVersionId).Take(2).ToArray();
        if (descriptionSources.Length != 1)
            return Failure(RequestErrorKind.Conflict,
                "The approved system-description version must be included in the Type I baseline.");
        var descriptionSource = descriptionSources[0];
        var description = await reader.HydrateAsync(new SystemDescriptionAggregate(request.TenantId,
            request.ProgramId, descriptionSource.RecordId), ct).ConfigureAwait(false);
        var descriptionView = description.Read();
        var descriptionVersion = description.FindVersion(content.SystemDescriptionVersionId);
        var approval = description.FindApproval(content.SystemDescriptionVersionId);
        var binding = descriptionView.Bindings.SingleOrDefault(item =>
            item.VersionId == content.SystemDescriptionVersionId && item.Purpose == "type_i_baseline");
        if (descriptionVersion is not { Status: "approved" } ||
            descriptionVersion.TenantId != request.TenantId ||
            descriptionVersion.ProgramId != request.ProgramId ||
            descriptionVersion.SystemDescriptionId != descriptionSource.RecordId ||
            descriptionVersion.Scope.Stage != "type_i" ||
            descriptionVersion.Scope.ExaminationId != request.ExaminationId ||
            descriptionVersion.Scope.TypeIBaselineId != baseline.TypeIBaselineId ||
            !HashEquals(descriptionVersion.ContentSha256, descriptionSource.ContentSha256) ||
            approval is null || approval.Version != descriptionVersion.Version || binding is null ||
            binding.Version != descriptionVersion.Version || binding.ExaminationId != request.ExaminationId ||
            binding.SnapshotId != baseline.TypeIBaselineId)
            return Failure(RequestErrorKind.Conflict,
                "The plan's system description must be approved and bound to the exact Type I baseline.");

        var now = clock.GetUtcNow();
        var planApproval = planView.Approvals.Single(item => item.PlanVersion == planView.CurrentVersionNumber);
        if (now == default || now < planApproval.ApprovedAt || now < baseline.UpdatedAt ||
            now < outcomeView.RecordedAt || now < descriptionVersion.UpdatedAt ||
            now < approval.ApprovedAt || now < binding.BoundAt ||
            planVersion.ProposedAt < baseline.UpdatedAt ||
            planVersion.ProposedAt < outcomeView.RecordedAt ||
            planVersion.ProposedAt < approval.ApprovedAt ||
            planVersion.ProposedAt < binding.BoundAt)
            return Failure(RequestErrorKind.Conflict,
                "The period cannot start before its approved plan and frozen source records exist.");

        var sourceReferences = baseline.Sources.Select(source => new ExaminationContentReference(
            source.Kind, source.RecordId, source.VersionId, source.Revision, source.ContentSha256))
            .OrderBy(source => source.SourceType, StringComparer.Ordinal)
            .ThenBy(source => source.RecordId.ToString(), StringComparer.Ordinal).ToArray();
        var acknowledgedGaps = content.AcknowledgedGaps.Select(item => item.GapId)
            .OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray();
        var openingManifest = new OpeningManifest(request.TenantId, request.ProgramId,
            request.ExaminationId, request.PeriodId, request.PlanId, planView.CurrentVersionNumber,
            planVersion.ContentSha256, baseline.TypeIBaselineId, baseline.ContentSha256,
            content.TypeIOutcomeId, outcomeView.ContentSha256,
            content.SystemDescriptionVersionId, descriptionVersion.ContentSha256,
            sourceReferences, acknowledgedGaps);
        var opening = new TypeIIPeriodOpeningContent(request.ExaminationId, request.PeriodId,
            Digest(openingManifest), sourceReferences, acknowledgedGaps);

        return await executor.ExecuteAsync(new TypeIIPeriod(request.TenantId, request.ProgramId,
                request.PeriodId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.Start(planView, opening, actorMemberId, actorDisplay, now)), context, ct)
            .ConfigureAwait(false);
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

    static bool ValidBaseline(TypeIBaselineView baseline)
    {
        var preview = TypeIBaselinePreview.Create(baseline.TenantId, baseline.ProgramId,
            baseline.ExaminationEngagementId, baseline.Sources, [], baseline.ScopeDifferences);
        return HashEquals(preview.ContentSha256, baseline.ContentSha256) &&
            preview.CanonicalManifest == baseline.CanonicalManifest;
    }

    static string Digest<T>(T value) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();

    static bool HashEquals(string? left, string? right) => left is not null && right is not null &&
        StringComparer.OrdinalIgnoreCase.Equals(left, right);

    static Result Failure(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));

    sealed record OpeningManifest(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId,
        Uuid OpeningSnapshotId, Uuid TypeIIPlanId, int TypeIIPlanVersion,
        string TypeIIPlanSha256, Uuid TypeIBaselineId, string TypeIBaselineSha256,
        Uuid TypeIOutcomeId, string TypeIOutcomeSha256, Uuid SystemDescriptionVersionId,
        string SystemDescriptionSha256, IReadOnlyList<ExaminationContentReference> Sources,
        IReadOnlyList<Uuid> AcknowledgedGapIds);
}

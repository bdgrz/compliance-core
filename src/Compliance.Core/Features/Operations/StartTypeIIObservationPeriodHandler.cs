using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.TypeIIPlan;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Starts a period from an approved plan and its exact frozen Type I source chain.</summary>
public sealed class StartTypeIIObservationPeriodHandler(IAggregateExecutor executor,
    IAggregateReader reader, IDomainEventReader events, TimeProvider clock)
    : IRequestHandler<StartTypeIIObservationPeriod>
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
        var now = clock.GetUtcNow();
        var checkedSources = await TypeIIPlanSourceValidator.ValidateAsync(reader,
            request.TenantId, request.ProgramId, request.ExaminationId, content, now, ct)
            .ConfigureAwait(false);
        if (!checkedSources.IsSuccess)
            return Result.Failure(checkedSources.Error!);
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

        var bootstrap = await TypeIIObservationCalendarBootstrap.EnsureAsync(executor, reader,
            events, context, request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
        if (!bootstrap.IsSuccess)
            return bootstrap;

        var period = await reader.HydrateAsync(new TypeIIPeriod(request.TenantId,
            request.ProgramId, request.PeriodId), ct).ConfigureAwait(false);
        if (period.Start(planView, opening, actorMemberId, actorDisplay, now) is { } startFailure)
            return Result.Failure(startFailure.ToRequestError());

        var reserved = await executor.ExecuteAsync(new TypeIIObservationCalendar(request.TenantId,
                request.ProgramId), calendar => CommandFailureRequestAdapter.ToOutcome(
                calendar.Reserve(request.PeriodId, request.ExaminationId, request.PlanId,
                    request.PeriodStart, request.PeriodEnd, opening.OpeningSnapshotSha256,
                    actorMemberId, actorDisplay, now)), context, ct)
            .ConfigureAwait(false);
        if (!reserved.IsSuccess)
            return reserved;

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

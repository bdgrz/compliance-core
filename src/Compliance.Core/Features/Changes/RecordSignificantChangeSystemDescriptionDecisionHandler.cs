using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.SystemDescription;

namespace Bdgrz.Compliance.Features.Changes;

public sealed class RecordSignificantChangeSystemDescriptionDecisionHandler(
    IAggregateExecutor executor, IAggregateReader reader)
    : IRequestHandler<RecordSignificantChangeSystemDescriptionDecision>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<RecordSignificantChangeSystemDescriptionDecision> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "System-description decisions may only be copied by a trusted reactor."));
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.ChangeId == Uuid.Empty || request.ContentVersion <= 0 ||
            request.SystemDescriptionId == Uuid.Empty || request.SystemDescriptionVersionId == Uuid.Empty ||
            request.DecisionId == Uuid.Empty || request.Decision is not ("approved_successor" or "no_change") ||
            request.AssessmentSha256 is not { Length: 64 } ||
            !request.AssessmentSha256.All(static character => Uri.IsHexDigit(character)))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The exact significant-change system-description decision is required."));

        var description = await reader.HydrateAsync(new SystemDescriptionAggregate(request.TenantId,
            request.ProgramId, request.SystemDescriptionId), ct).ConfigureAwait(false);
        var decision = SystemDescriptionDecisionCorrelation.Find(description, request.TenantId,
            request.ProgramId, request.ChangeId, request.SystemDescriptionVersionId,
            request.DecisionId, request.Decision, request.AssessmentSha256);
        if (decision is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The requested description decision is not an exact approved successor or no-change fact for this change."));

        return await executor.ExecuteAsync(new SignificantChange(request.TenantId,
                request.ProgramId, request.ChangeId), change =>
            {
                if (Classify(change, request, decision) is { } outcome)
                    return AggregateOutcome.Discard(Result.Failure(outcome));
                if (change.Current?.CurrentAssessment is not { RequiresSystemDescriptionReview: true } assessment ||
                    assessment.ContentVersion != request.ContentVersion ||
                    SignificantChange.AssessmentSha256(assessment) != request.AssessmentSha256 ||
                    decision.DecidedAt < assessment.AssessedAt)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "The system-description decision does not match the current change assessment.")));
                return CommandFailureRequestAdapter.ToOutcome(change.RecordSystemDescriptionDecision(
                    change.Revision, decision.Decision, decision.DecisionId, decision.Rationale,
                    decision.ActorMemberId, decision.ActorDisplay, decision.DecidedAt));
            }, context, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Separates a decision that can still be applied from one whose assessment is superseded or already decided
    ///     (terminal) and one whose assessment or required review-work links are not visible yet (transient).
    /// </summary>
    static RequestError? Classify(SignificantChange change, RecordSignificantChangeSystemDescriptionDecision request,
        CorrelatedSystemDescriptionDecision decision)
    {
        if (change.Current is not { } current)
            return null;
        var retained = current.Assessments.FirstOrDefault(item => item.ContentVersion == request.ContentVersion);
        if (retained is not null && SignificantChange.AssessmentSha256(retained) != request.AssessmentSha256)
            return null;
        if (current.SystemDescriptionDecisions.Any(item => item.DecisionId == request.DecisionId))
            return null;
        var outcome = ImpactReactionOutcomes.ClassifyDecision(request.ContentVersion, current.CurrentVersionNumber,
            retained is not null,
            current.SystemDescriptionDecisions.Any(item => item.ContentVersion == request.ContentVersion),
            decision.DecidedAt, retained?.AssessedAt ?? default);
        if (outcome is not null)
            return outcome;
        return retained is not null && retained.RequiresSystemDescriptionReview &&
               retained.RelatedReviewWorkIds.Concat(retained.RelatedCorrectiveActionIds).Any(id =>
                   !current.ReviewWorkLinks.Any(link => link.ContentVersion == request.ContentVersion &&
                                                        link.WorkId == id))
            ? ImpactReactionOutcomes.NotReady("Required follow-up work for the description review is not linked yet.")
            : null;
    }
}

using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

public sealed class RecordSignificantChangeSystemDescriptionDecisionHandler(
    SignificantChangeOperations operations, SystemDescriptionReader descriptions)
    : IRequestHandler<RecordSignificantChangeSystemDescriptionDecision, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(
        IRequestContext<RecordSignificantChangeSystemDescriptionDecision> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return ValueTask.FromResult(Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "System-description decisions may only be copied by a trusted reactor.")));
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.ChangeId == Uuid.Empty || request.ContentVersion <= 0 ||
            request.SystemDescriptionId == Uuid.Empty || request.SystemDescriptionVersionId == Uuid.Empty ||
            request.DecisionId == Uuid.Empty || request.Decision is not ("approved_successor" or "no_change") ||
            request.AssessmentSha256 is not { Length: 64 } ||
            !request.AssessmentSha256.All(static character => Uri.IsHexDigit(character)))
            return ValueTask.FromResult(Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Validation,
                "The exact significant-change system-description decision is required.")));

        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.ChangeId, request.CommandId,
            nameof(RecordSignificantChangeSystemDescriptionDecision), null,
            ActorReference.ForSystemProcess("reactor:SystemDescriptionDecision", "SystemDescriptionDecision"),
            async (snapshot, operation, token) =>
            {
                SystemDescriptionReadSnapshot description;
                try
                {
                    description = await descriptions.ReadAsync(request.TenantId, request.ProgramId, request.SystemDescriptionId, token).ConfigureAwait(false);
                }
                catch (ImmutableContentIntegrityException failure)
                {
                    return Result.Failure(SystemDescriptionReadFailure.Of(failure));
                }
                var decision = SystemDescriptionDecisionCorrelation.Find(description, request.TenantId, request.ProgramId,
                    request.ChangeId, request.SystemDescriptionVersionId, request.DecisionId, request.Decision, request.AssessmentSha256);
                if (decision is null)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The description decision is not an exact approved successor or no-change fact for this change."));
                if (Classify(snapshot.View, request, decision) is { } outcome)
                    return Result.Failure(outcome);
                if (snapshot.View?.CurrentAssessment is not { RequiresSystemDescriptionReview: true } assessment ||
                    assessment.ContentVersion != request.ContentVersion ||
                    SignificantChange.AssessmentSha256(assessment) != request.AssessmentSha256 || decision.DecidedAt < assessment.AssessedAt)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The description decision does not match the current change assessment."));
                return SignificantChangeOperations.Outcome(snapshot.Rules.RecordSystemDescriptionDecision(snapshot.Rules.Revision,
                    decision.Decision, decision.DecisionId, decision.Rationale, decision.ActorMemberId, decision.ActorDisplay, decision.DecidedAt));
            }, ct);
    }

    /// <summary>
    ///     Separates a decision that can still be applied from one whose assessment is superseded or already decided
    ///     (terminal) and one whose assessment or required review-work links are not visible yet (transient).
    /// </summary>
    static RequestError? Classify(SignificantChangeView? current, RecordSignificantChangeSystemDescriptionDecision request,
        CorrelatedSystemDescriptionDecision decision)
    {
        if (current is null)
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

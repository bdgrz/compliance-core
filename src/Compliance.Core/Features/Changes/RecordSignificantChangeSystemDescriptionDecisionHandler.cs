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
}

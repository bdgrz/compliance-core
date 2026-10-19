using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.SystemDescription;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class RecordComplianceIncidentSystemDescriptionDecisionHandler(
    IAggregateExecutor executor, IAggregateReader reader)
    : IRequestHandler<RecordComplianceIncidentSystemDescriptionDecision>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<RecordComplianceIncidentSystemDescriptionDecision> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "System-description decisions may only be copied by a trusted reactor."));
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.IncidentId == Uuid.Empty || request.ContentVersion <= 0 ||
            request.SystemDescriptionId == Uuid.Empty || request.SystemDescriptionVersionId == Uuid.Empty ||
            request.DecisionId == Uuid.Empty || request.AssessmentSha256 is not { Length: 64 } ||
            !request.AssessmentSha256.All(static character => Uri.IsHexDigit(character)))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The exact incident no-change decision is required."));

        var description = await reader.HydrateAsync(new SystemDescriptionAggregate(request.TenantId,
            request.ProgramId, request.SystemDescriptionId), ct).ConfigureAwait(false);
        var decision = SystemDescriptionDecisionCorrelation.Find(description, request.TenantId,
            request.ProgramId, request.IncidentId, request.SystemDescriptionVersionId,
            request.DecisionId, "no_change", request.AssessmentSha256);
        if (decision is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The requested no-change fact is not correlated to this incident."));

        return await executor.ExecuteAsync(new ComplianceIncident(request.TenantId,
                request.ProgramId, request.IncidentId), incident =>
            {
                if (incident.Current?.CurrentAssessment is not
                    { RequiresSystemDescriptionReview: true } assessment ||
                    assessment.ContentVersion != request.ContentVersion ||
                    ComplianceIncident.AssessmentSha256(assessment) != request.AssessmentSha256 ||
                    decision.DecidedAt < assessment.AssessedAt)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "The no-change decision does not match the current incident assessment.")));
                return CommandFailureRequestAdapter.ToOutcome(incident.RecordSystemDescriptionDecision(
                    incident.Revision, decision.Decision, decision.DecisionId, decision.Rationale,
                    decision.ActorMemberId, decision.ActorDisplay, decision.DecidedAt));
            }, context, ct).ConfigureAwait(false);
    }
}

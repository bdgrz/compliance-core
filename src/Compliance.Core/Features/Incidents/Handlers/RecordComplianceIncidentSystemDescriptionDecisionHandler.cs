using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.Incidents.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Readers;
using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SystemDescription;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents.Handlers;

public sealed class RecordComplianceIncidentSystemDescriptionDecisionHandler(IncidentOperations operations, SystemDescriptionReader descriptions)
    : IRequestHandler<RecordComplianceIncidentSystemDescriptionDecision>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordComplianceIncidentSystemDescriptionDecision> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden, "System-description decisions may only be copied by a trusted reactor.")));
        if (request.ContentVersion < 1 || request.SystemDescriptionId == Uuid.Empty || request.SystemDescriptionVersionId == Uuid.Empty ||
            request.DecisionId == Uuid.Empty || request.AssessmentSha256 is not { Length: 64 } || !request.AssessmentSha256.All(Uri.IsHexDigit))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation, "The exact incident no-change decision is required.")));
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.IncidentId, request.CommandId,
            nameof(RecordComplianceIncidentSystemDescriptionDecision), null, ActorReference.ForSystemProcess("system", "system"), async (snapshot, operation, token) =>
            {
                SystemDescriptionReadSnapshot source;
                try
                { source = await descriptions.ReadAsync(request.TenantId, request.ProgramId, request.SystemDescriptionId, token).ConfigureAwait(false); }
                catch (ImmutableContentIntegrityException exception) { return Result.Failure(SystemDescriptionReadFailure.Of(exception)); }
                var decision = SystemDescriptionDecisionCorrelation.Find(source, request.TenantId, request.ProgramId, request.IncidentId,
                    request.SystemDescriptionVersionId, request.DecisionId, "no_change", request.AssessmentSha256);
                if (decision is null || snapshot.View is not { } current)
                    return Conflict();
                var retained = snapshot.Authority?.Assessments.FirstOrDefault(item => item.ContentVersion == request.ContentVersion);
                if (retained is not null && retained.AssessmentSha256 != request.AssessmentSha256)
                    return Conflict();
                var outcome = ImpactReactionOutcomes.ClassifyDecision(request.ContentVersion, current.CurrentVersionNumber, retained is not null,
                    current.SystemDescriptionDecisions.Any(item => item.ContentVersion == request.ContentVersion), decision.DecidedAt, retained?.AssessedAt ?? default);
                if (outcome is not null)
                    return Result.Failure(outcome);
                if (current.CurrentAssessment is not { RequiresSystemDescriptionReview: true } assessment || assessment.ContentVersion != request.ContentVersion ||
                    ComplianceIncident.AssessmentSha256(assessment) != request.AssessmentSha256 || decision.DecidedAt < assessment.AssessedAt)
                    return Conflict();
                return IncidentOperations.Outcome(snapshot.Rules.RecordSystemDescriptionDecision(decision.Decision, decision.DecisionId,
                    decision.Rationale, decision.ActorMemberId, decision.ActorDisplay, decision.DecidedAt));
            }, ct);
    }
    static Result Conflict() => Result.Failure(new RequestError(RequestErrorKind.Conflict, "The no-change decision does not match the current incident assessment."));
}

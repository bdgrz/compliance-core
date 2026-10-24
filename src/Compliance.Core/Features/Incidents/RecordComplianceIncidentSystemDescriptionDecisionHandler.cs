using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SystemDescription;
using Cntryl.Portia;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.SystemDescription;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class RecordComplianceIncidentSystemDescriptionDecisionHandler(NativeOperationExecutor operations,
    IAggregateReader reader, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RecordComplianceIncidentSystemDescriptionDecision>
{
    const string CommandKind = "RecordComplianceIncidentSystemDescriptionDecision";

    public async ValueTask<Result> HandleAsync(
        IRequestContext<RecordComplianceIncidentSystemDescriptionDecision> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "System-description decisions may only be copied by a trusted reactor."));
        var request = context.Request;
        if (IncidentOperations.Validate(request.CommandId, request.TenantId, request.ProgramId, request.IncidentId,
                null) is { } invalid)
            return Result.Failure(invalid);
        if (request.ContentVersion <= 0 ||
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

        var provenance = IncidentOperations.Provenance(request, CommandKind, request.CommandId, request.TenantId,
            request.ProgramId, request.IncidentId, null, ActorReference.ForSystemProcess("system", "system"),
            clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var observed = await reader.HydrateAsync(IncidentOperations.Owner(request.TenantId, request.ProgramId,
            request.IncidentId, request.CommandId), ct).ConfigureAwait(false);
        if (observed.Acceptance.Decide(binding) == NativeAcceptanceDecision.None &&
            Classify(observed, request, decision) is { } outcome)
            return Result.Failure(outcome);

        var response = await IncidentOperations.UnitResponseAsync(content, request.TenantId, ct).ConfigureAwait(false);
        var accepted = await operations.ExecuteAsync(new NativeOperation<ComplianceIncident>(binding,
            () => IncidentOperations.Owner(request.TenantId, request.ProgramId, request.IncidentId, request.CommandId),
            incident => incident.Acceptance,
            incident => Prepare(incident, request, decision, response),
            (ComplianceIncident incident, ImmutableContentRef frozen) => Guard(incident, request, decision) is { } mismatch
                ? Result.Failure(mismatch)
                : IncidentOperations.Applied(incident.RecordSystemDescriptionDecision(provenance, frozen,
                    decision.Decision, decision.DecisionId, decision.Rationale, decision.ActorMemberId,
                    decision.ActorDisplay, decision.DecidedAt))), context, ct).ConfigureAwait(false);
        return IncidentOperations.Done(accepted);
    }

    /// <summary>
    ///     Separates a decision that can still be applied from one whose assessment is superseded or already decided
    ///     (terminal) and one whose assessment is not visible yet (transient).
    /// </summary>
    static RequestError? Classify(ComplianceIncident incident, RecordComplianceIncidentSystemDescriptionDecision request,
        CorrelatedSystemDescriptionDecision decision)
    {
        if (incident.Current is not { } current)
            return new RequestError(RequestErrorKind.Conflict,
                "The no-change decision does not match the current incident assessment.");
        var retained = current.Assessments.FirstOrDefault(item => item.ContentVersion == request.ContentVersion);
        if (retained is not null && ComplianceIncident.AssessmentSha256(retained) != request.AssessmentSha256)
            return new RequestError(RequestErrorKind.Conflict,
                "The no-change decision does not match the current incident assessment.");
        return ImpactReactionOutcomes.ClassifyDecision(request.ContentVersion, current.CurrentVersionNumber,
            retained is not null,
            current.SystemDescriptionDecisions.Any(item => item.ContentVersion == request.ContentVersion),
            decision.DecidedAt, retained?.AssessedAt ?? default);
    }

    static Result<FirstResponseDescriptor> Prepare(ComplianceIncident incident,
        RecordComplianceIncidentSystemDescriptionDecision request, CorrelatedSystemDescriptionDecision decision,
        FirstResponseDescriptor response)
    {
        if (Guard(incident, request, decision) is { } mismatch)
            return Result<FirstResponseDescriptor>.Failure(mismatch);
        return IncidentOperations.Prepared(incident.PrepareSystemDescriptionDecision(decision.Decision,
            decision.DecisionId, decision.Rationale, decision.ActorMemberId, decision.ActorDisplay,
            decision.DecidedAt), response);
    }

    static RequestError? Guard(ComplianceIncident incident,
        RecordComplianceIncidentSystemDescriptionDecision request, CorrelatedSystemDescriptionDecision decision) =>
        incident.Current?.CurrentAssessment is not { RequiresSystemDescriptionReview: true } assessment ||
        assessment.ContentVersion != request.ContentVersion ||
        ComplianceIncident.AssessmentSha256(assessment) != request.AssessmentSha256 ||
        decision.DecidedAt < assessment.AssessedAt
            ? new RequestError(RequestErrorKind.Conflict,
                "The no-change decision does not match the current incident assessment.")
            : null;
}

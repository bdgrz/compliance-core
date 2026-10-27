using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class GetComplianceImpactAssessmentHandler(ComplianceIncidentReader incidents, RestrictedComplianceRecordVisibility visibility,
    ComplianceIncidentReadConsistency consistency) : IRequestHandler<GetComplianceImpactAssessment, ComplianceIncidentImpactAssessmentView>
{
    public async ValueTask<Result<ComplianceIncidentImpactAssessmentView>> HandleAsync(IRequestContext<GetComplianceImpactAssessment> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.IncidentId == Uuid.Empty)
            return Result<ComplianceIncidentImpactAssessmentView>.Failure(new RequestError(RequestErrorKind.Validation, "Tenant, program and incident identities are required."));
        try
        {
            var snapshot = await incidents.ReadAsync(request.TenantId, request.ProgramId, request.IncidentId, ct).ConfigureAwait(false);
            if (snapshot.View is not { Current: { } version, CurrentAssessment: { } assessment } view ||
                !await visibility.CanReadAsync(request.TenantId, context.Actor, version.Content.HandlingClass,
                    view.Assessments.Select(item => item.AssessedByMemberId).ToArray(), ct).ConfigureAwait(false))
                return Result<ComplianceIncidentImpactAssessmentView>.Failure(new RequestError(RequestErrorKind.NotFound, "The current compliance-incident assessment was not found."));
            var confirmed = await consistency.ConfirmAsync(snapshot, ct).ConfigureAwait(false);
            return confirmed.IsSuccess ? Result<ComplianceIncidentImpactAssessmentView>.Success(assessment) : Result<ComplianceIncidentImpactAssessmentView>.Failure(confirmed.Error!);
        }
        catch (ImmutableContentIntegrityException exception)
        { return Result<ComplianceIncidentImpactAssessmentView>.Failure(IncidentOperations.ContentFailure(exception)); }
    }
}

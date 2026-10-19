using Cntryl.Portia;
using Bdgrz.Compliance.Features.Changes;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class GetComplianceImpactAssessmentHandler(
    IComplianceIncidentDirectoryReader directory,
    RestrictedComplianceRecordVisibility visibility)
    : IRequestHandler<GetComplianceImpactAssessment, ComplianceIncidentImpactAssessmentView>
{
    public async ValueTask<Result<ComplianceIncidentImpactAssessmentView>> HandleAsync(
        IRequestContext<GetComplianceImpactAssessment> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.IncidentId == Uuid.Empty)
            return Result<ComplianceIncidentImpactAssessmentView>.Failure(new RequestError(
                RequestErrorKind.Validation, "Tenant, program, and incident identities are required."));

        var incident = await directory.GetAsync(request.TenantId, request.IncidentId, ct)
            .ConfigureAwait(false);
        if (incident is not { Current: { } current } view || view.TenantId != request.TenantId ||
            view.ProgramId != request.ProgramId || view.ComplianceIncidentId != request.IncidentId ||
            view.CurrentAssessment is not { } assessment ||
            !await visibility.CanReadAsync(request.TenantId, context.Actor,
                current.Content.HandlingClass,
                view.Assessments.Select(static item => item.AssessedByMemberId).ToArray(), ct)
                .ConfigureAwait(false))
            return Result<ComplianceIncidentImpactAssessmentView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The current compliance-incident assessment was not found."));
        return Result<ComplianceIncidentImpactAssessmentView>.Success(assessment);
    }
}

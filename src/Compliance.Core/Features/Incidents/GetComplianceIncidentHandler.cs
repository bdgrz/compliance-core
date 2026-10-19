using Cntryl.Portia;
using Bdgrz.Compliance.Features.Changes;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class GetComplianceIncidentHandler(IComplianceIncidentDirectoryReader directory,
    RestrictedComplianceRecordVisibility visibility)
    : IRequestHandler<GetComplianceIncident, ComplianceIncidentView>
{
    public async ValueTask<Result<ComplianceIncidentView>> HandleAsync(
        IRequestContext<GetComplianceIncident> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.IncidentId == Uuid.Empty)
            return Result<ComplianceIncidentView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Tenant, program, and incident identities are required."));

        var incident = await directory.GetAsync(request.TenantId,
            request.IncidentId, ct).ConfigureAwait(false);
        if (incident is not { Current: { } current } view || view.TenantId != request.TenantId ||
            view.ProgramId != request.ProgramId || view.ComplianceIncidentId != request.IncidentId ||
            !await visibility.CanReadAsync(request.TenantId, context.Actor,
                current.Content.HandlingClass,
                view.Assessments.Select(static assessment => assessment.AssessedByMemberId).ToArray(), ct)
                .ConfigureAwait(false))
            return Result<ComplianceIncidentView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The compliance incident was not found."));
        return Result<ComplianceIncidentView>.Success(view);
    }
}

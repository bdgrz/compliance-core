using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class GetComplianceIncidentHandler(ComplianceIncidentReader changes,
    RestrictedComplianceRecordVisibility visibility, ComplianceIncidentReadConsistency consistency)
    : IRequestHandler<GetComplianceIncident, ComplianceIncidentView>
{
    public async ValueTask<Result<ComplianceIncidentView>> HandleAsync(
        IRequestContext<GetComplianceIncident> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.IncidentId == Uuid.Empty)
            return Result<ComplianceIncidentView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Tenant, program, and compliance-incident identities are required."));

        try
        {
            var snapshot = await changes.ReadAsync(request.TenantId, request.ProgramId, request.IncidentId, ct).ConfigureAwait(false);
            if (snapshot.View is not { Current: { } current } view ||
                !await visibility.CanReadAsync(request.TenantId, context.Actor, current.Content.HandlingClass,
                    view.Assessments.Select(item => item.AssessedByMemberId).ToArray(), ct).ConfigureAwait(false))
                return Result<ComplianceIncidentView>.Failure(new RequestError(RequestErrorKind.NotFound, "The compliance incident was not found."));
            var confirmed = await consistency.ConfirmAsync(snapshot, ct).ConfigureAwait(false);
            return confirmed.IsSuccess ? Result<ComplianceIncidentView>.Success(view) : Result<ComplianceIncidentView>.Failure(confirmed.Error!);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ComplianceIncidentView>.Failure(IncidentOperations.ContentFailure(exception));
        }
    }
}

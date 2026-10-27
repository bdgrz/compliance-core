using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Admits a signed revocation assertion as a 202 root change; it never requires active staff.</summary>
sealed class RevokeFirmProfessionalDutyDesignationHandler(ProfessionalDutyAdmission admission)
    : IRequestHandler<RevokeFirmProfessionalDutyDesignation, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<RevokeFirmProfessionalDutyDesignation> context, CancellationToken ct)
    {
        var request = context.Request;
        return admission.AdmitAsync(context, ProfessionalDutyChangeKind.Revoke, request.DesignationId,
            request.AssertionId, request.SignedAssertion, request.ExpectedRevision, ct);
    }
}

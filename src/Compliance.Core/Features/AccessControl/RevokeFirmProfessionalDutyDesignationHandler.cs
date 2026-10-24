using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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

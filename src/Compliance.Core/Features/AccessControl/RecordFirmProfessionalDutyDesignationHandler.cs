using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Admits a signed active assertion as a 202 root change of the designation owner.</summary>
sealed class RecordFirmProfessionalDutyDesignationHandler(ProfessionalDutyAdmission admission)
    : IRequestHandler<RecordFirmProfessionalDutyDesignation, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<RecordFirmProfessionalDutyDesignation> context, CancellationToken ct)
    {
        var request = context.Request;
        return admission.AdmitAsync(context, ProfessionalDutyChangeKind.Record, request.DesignationId,
            request.AssertionId, request.SignedAssertion, request.ExpectedRevision, ct);
    }
}

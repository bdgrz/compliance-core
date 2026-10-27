using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

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

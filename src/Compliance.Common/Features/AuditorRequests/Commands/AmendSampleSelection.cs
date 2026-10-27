using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Commands;

/// <summary>
///     Admits a selection that supersedes a prior admitted selection of the same population with a strictly later
///     population version. The earlier selection stays; the new one is a new independent owner.
/// </summary>
[Discriminator("bdgrz.audit-populations.amend-sample-selection", 2)]
public sealed record AmendSampleSelection(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId, Uuid AuditorRequestId,
    long ExpectedRequestRevision, Uuid SelectionId, Uuid SupersedesSelectionId, Uuid PopulationId,
    int PopulationVersion, IReadOnlyList<Uuid> PopulationRowIds, string SelectionMethod, string Source,
    string ExternalReference, string AmendmentReason)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => SelectionId;
}

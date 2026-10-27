using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Commands;

/// <summary>
///     Admits an exact frozen population version and a nonempty duplicate-free row selection on the open request.
///     <paramref name="ExpectedRequestRevision" /> is compared on the request; the selection owner is then created
///     independently at its own first revision.
/// </summary>
[Discriminator("bdgrz.audit-populations.record-sample-selection", 2)]
public sealed record RecordSampleSelection(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId, Uuid AuditorRequestId,
    long ExpectedRequestRevision, Uuid SelectionId, Uuid PopulationId, int PopulationVersion,
    IReadOnlyList<Uuid> PopulationRowIds, string SelectionMethod, string Source, string ExternalReference)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => SelectionId;
}

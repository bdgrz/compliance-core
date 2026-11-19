using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>A current Compliance Lead cancels open operating work with a retained reason.</summary>
[Discriminator("bdgrz.control.occurrence.cancel", 1)]
public sealed record CancelControlOccurrence(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid OccurrenceId, long ExpectedRevision, string Reason)
    : IRequest<ControlOccurrenceView>, IControlOperationRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

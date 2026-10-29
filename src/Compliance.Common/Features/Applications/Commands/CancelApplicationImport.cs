using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
namespace Bdgrz.Compliance.Features.Applications.Commands;

/// <summary>
///     Cancels a batch that has not published. The batch records the cancellation first; every target is then fenced
///     (including targets whose hold was never acquired) before any hold is released. A published batch is
///     irreversible and the cancellation conflicts.
/// </summary>
[Discriminator("bdgrz.application_import.cancel", 2)]
public sealed record CancelApplicationImport(Uuid CommandId, Uuid TenantId, Uuid BatchId,
    long ExpectedRevision, string Reason)
    : IRequest<OperationAcceptedResponse>, IApplicationInventoryWriteRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

/// <summary>
///     Revises an operational process. <paramref name="CommandId" /> is the logical identity of the operation and
///     <paramref name="ExpectedRevision" /> the revision of the process the caller observed. The answer is the fixed
///     202 acknowledgment while the name claim and the record fact follow.
/// </summary>
[Discriminator("bdgrz.inventory.operational_process.revise", 1)]
public sealed record ReviseOperationalProcess(Uuid CommandId, Uuid TenantId, Uuid OperationalProcessId,
    long ExpectedRevision, string Name, string Purpose, Uuid OwnerPersonId, string Lifecycle,
    string? Inputs = null, string? Outputs = null)
    : IRequest<OperationAcceptedResponse>, ITechnologyInventoryWriteRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

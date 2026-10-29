using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

/// <summary>
///     Records a governed operational process in the tenant's inventory. <paramref name="OperationalProcessId" /> is
///     the stable identity of the process and the logical identity of the creation; an exact retry replays the
///     original acceptance. The answer is the fixed 202 acknowledgment while the name claim and the record fact follow.
/// </summary>
[Discriminator("bdgrz.inventory.operational_process.record", 1)]
public sealed record RecordOperationalProcess(Uuid OperationalProcessId, Uuid TenantId, string Name, string Purpose,
    Uuid OwnerPersonId, string? Inputs = null, string? Outputs = null)
    : IRequest<OperationAcceptedResponse>, ITechnologyInventoryWriteRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => OperationalProcessId;
}

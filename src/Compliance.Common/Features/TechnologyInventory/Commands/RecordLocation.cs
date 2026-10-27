using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

/// <summary>
///     Records a governed location in the tenant's inventory. <paramref name="LocationId" /> is the stable identity of
///     the location and the logical identity of the creation; an exact retry replays the original acceptance. The
///     answer is the fixed 202 acknowledgment while the name claim and the record fact follow.
/// </summary>
[Discriminator("bdgrz.inventory.location.record", 1)]
public sealed record RecordLocation(Uuid LocationId, Uuid TenantId, string Kind, string Name, Uuid OwnerPersonId,
    string? GeographyReference = null)
    : IRequest<OperationAcceptedResponse>, ITechnologyInventoryWriteRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => LocationId;
}

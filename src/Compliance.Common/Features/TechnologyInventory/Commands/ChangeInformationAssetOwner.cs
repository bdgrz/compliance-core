using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

[Discriminator("bdgrz.inventory.information_asset.change_owner", 1)]
public sealed record ChangeInformationAssetOwner(Uuid CommandId, Uuid TenantId, Uuid InformationAssetId,
    long ExpectedRevision, Uuid OwnerPersonId) : IRequest, IInformationAssetChange, ITechnologyInventoryLeadWriteRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

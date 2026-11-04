using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

[Discriminator("bdgrz.inventory.information_asset.classify", 1)]
public sealed record ClassifyInformationAsset(Uuid CommandId, Uuid TenantId, Uuid InformationAssetId,
    long ExpectedRevision, string Classification) : IRequest, IInformationAssetChange, ITechnologyInventoryLeadWriteRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

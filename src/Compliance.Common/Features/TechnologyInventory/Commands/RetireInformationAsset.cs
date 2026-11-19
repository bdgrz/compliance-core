using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

[Discriminator("bdgrz.inventory.information_asset.retire", 1)]
public sealed record RetireInformationAsset(Uuid CommandId, Uuid TenantId, Uuid InformationAssetId,
    long ExpectedRevision, string Reason, DateTimeOffset? EffectiveAt = null) : IRequest, IInformationAssetChange, ITechnologyInventoryLeadWriteRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

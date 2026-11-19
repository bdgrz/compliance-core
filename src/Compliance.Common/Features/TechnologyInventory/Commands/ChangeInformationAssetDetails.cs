using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

[Discriminator("bdgrz.inventory.information_asset.change_details", 1)]
public sealed record ChangeInformationAssetDetails(Uuid CommandId, Uuid TenantId, Uuid InformationAssetId,
    long ExpectedRevision, string Name, string RetentionReference, string? Description = null) : IRequest, IInformationAssetChange, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

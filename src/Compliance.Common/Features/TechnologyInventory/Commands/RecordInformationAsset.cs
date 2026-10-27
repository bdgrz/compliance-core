using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

/// <summary>
///     Records a governed information asset in the tenant's manual inventory. <paramref name="InformationAssetId" />
///     is the stable identity of the asset and the logical identity of the creation; an exact retry replays the
///     original answer even after later revisions.
/// </summary>
[Discriminator("bdgrz.inventory.information_asset.record", 1)]
public sealed record RecordInformationAsset(Uuid InformationAssetId, Uuid TenantId, string Name,
    string Classification, string RetentionReference, Uuid OwnerPersonId, string? Description = null)
    : IRequest<InformationAssetRegistration>, ITechnologyInventoryWriteRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => InformationAssetId;
}

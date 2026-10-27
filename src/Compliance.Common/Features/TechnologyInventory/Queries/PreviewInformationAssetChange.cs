using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Queries;

/// <summary>
/// Previews the data-flow impact of reclassifying or retiring an information asset. It records
/// nothing; omitted fields keep the asset's current value.
/// </summary>
[Discriminator("bdgrz.inventory.information_asset.change.preview", 1)]
public sealed record PreviewInformationAssetChange(Uuid TenantId, Uuid InformationAssetId,
    long ExpectedRevision, string? Classification = null, string? Lifecycle = null)
    : IRequest<InformationAssetChangePreview>, ITechnologyInventoryReadRequest, ICallable;

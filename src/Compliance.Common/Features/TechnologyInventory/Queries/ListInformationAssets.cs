using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Queries;

[Discriminator("bdgrz.inventory.information_asset.list", 1)]
public sealed record ListInformationAssets(Uuid TenantId, int? Limit = null,
    string? Cursor = null)
    : IRequest<Page<InformationAssetView>>, ITechnologyInventoryReadRequest, ICallable;

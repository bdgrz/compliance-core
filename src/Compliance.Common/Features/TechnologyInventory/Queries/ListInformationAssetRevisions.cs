using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Queries;

[Discriminator("bdgrz.inventory.information_asset.revision.list", 1)]
public sealed record ListInformationAssetRevisions(Uuid TenantId, Uuid InformationAssetId,
    int? Limit = null, string? Cursor = null, long? MinimumRevision = null)
    : IRequest<Page<InformationAssetView>>, ITechnologyInventoryReadRequest, ICallable;

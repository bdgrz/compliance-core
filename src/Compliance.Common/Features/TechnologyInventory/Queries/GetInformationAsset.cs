using Cntryl.Portia;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Queries;

[Discriminator("bdgrz.inventory.information_asset.get", 1)]
public sealed record GetInformationAsset(Uuid TenantId, Uuid InformationAssetId,
    long? MinimumRevision = null)
    : IRequest<InformationAssetView>, ITechnologyInventoryReadRequest, ICallable;

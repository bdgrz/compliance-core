using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Queries;

[Discriminator("bdgrz.inventory.location.list", 1)]
public sealed record ListLocations(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<LocationView>>, ITechnologyInventoryRequest, ICallable;

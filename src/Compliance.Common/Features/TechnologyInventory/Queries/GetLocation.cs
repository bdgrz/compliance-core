using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Queries;

[Discriminator("bdgrz.inventory.location.get", 1)]
public sealed record GetLocation(Uuid TenantId, Uuid LocationId, long? MinimumRevision = null)
    : IRequest<LocationView>, ITechnologyInventoryRequest, ICallable;

using Cntryl.Portia;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Queries;

[Discriminator("bdgrz.inventory.location.get", 1)]
public sealed record GetLocation(Uuid TenantId, Uuid LocationId, long? MinimumRevision = null)
    : IRequest<LocationView>, ITechnologyInventoryRequest, ICallable;

using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Queries;

[Discriminator("bdgrz.inventory.component.get", 1)]
public sealed record GetTechnologyComponent(Uuid TenantId, Uuid ComponentId,
    long? MinimumRevision = null)
    : IRequest<TechnologyComponentView>, ITechnologyInventoryRequest, ICallable;

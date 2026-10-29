using Cntryl.Portia;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Queries;

[Discriminator("bdgrz.inventory.component.get", 1)]
public sealed record GetTechnologyComponent(Uuid TenantId, Uuid ComponentId,
    long? MinimumRevision = null)
    : IRequest<TechnologyComponentView>, ITechnologyInventoryRequest, ICallable;

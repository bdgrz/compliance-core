using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Queries;

[Discriminator("bdgrz.inventory.operational_process.get", 1)]
public sealed record GetOperationalProcess(Uuid TenantId, Uuid OperationalProcessId, long? MinimumRevision = null)
    : IRequest<OperationalProcessView>, ITechnologyInventoryRequest, ICallable;

using Cntryl.Portia;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Queries;

[Discriminator("bdgrz.inventory.operational_process.get", 1)]
public sealed record GetOperationalProcess(Uuid TenantId, Uuid OperationalProcessId, long? MinimumRevision = null)
    : IRequest<OperationalProcessView>, ITechnologyInventoryRequest, ICallable;

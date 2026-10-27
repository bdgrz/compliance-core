using Cntryl.Portia;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Queries;

[Discriminator("bdgrz.inventory.data_flow.get", 1)]
public sealed record GetDataFlow(Uuid TenantId, Uuid DataFlowId, long? MinimumRevision = null)
    : IRequest<DataFlowView>, ITechnologyInventoryReadRequest, ICallable;

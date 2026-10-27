using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Queries;

[Discriminator("bdgrz.inventory.data_flow.list", 1)]
public sealed record ListDataFlows(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<DataFlowView>>, ITechnologyInventoryReadRequest, ICallable;

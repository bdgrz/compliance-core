using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Queries;

[Discriminator("bdgrz.inventory.data_flow.revision.list", 1)]
public sealed record ListDataFlowRevisions(Uuid TenantId, Uuid DataFlowId, int? Limit = null,
    string? Cursor = null, long? MinimumRevision = null)
    : IRequest<Page<DataFlowView>>, ITechnologyInventoryReadRequest, ICallable;

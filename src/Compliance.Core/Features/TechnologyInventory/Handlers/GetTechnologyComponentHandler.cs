using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

public sealed class GetTechnologyComponentHandler(TechnologyInventoryReadConsistency consistency)
    : IRequestHandler<GetTechnologyComponent, TechnologyComponentView>
{
    public ValueTask<Result<TechnologyComponentView>> HandleAsync(IRequestContext<GetTechnologyComponent> context,
        CancellationToken ct) =>
        consistency.GetComponentAsync(context.Request.TenantId, context.Request.ComponentId,
            context.Request.MinimumRevision, ct);
}

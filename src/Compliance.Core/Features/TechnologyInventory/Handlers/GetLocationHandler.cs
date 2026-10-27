using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

public sealed class GetLocationHandler(InventoryRegisterReadConsistency consistency)
    : IRequestHandler<GetLocation, LocationView>
{
    public ValueTask<Result<LocationView>> HandleAsync(IRequestContext<GetLocation> context,
        CancellationToken ct) =>
        consistency.GetLocationAsync(context.Request.TenantId, context.Request.LocationId,
            context.Request.MinimumRevision, ct);
}

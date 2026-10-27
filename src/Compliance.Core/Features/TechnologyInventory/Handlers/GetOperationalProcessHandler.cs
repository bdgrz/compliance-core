using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

public sealed class GetOperationalProcessHandler(InventoryRegisterReadConsistency consistency)
    : IRequestHandler<GetOperationalProcess, OperationalProcessView>
{
    public ValueTask<Result<OperationalProcessView>> HandleAsync(IRequestContext<GetOperationalProcess> context,
        CancellationToken ct) =>
        consistency.GetOperationalProcessAsync(context.Request.TenantId, context.Request.OperationalProcessId,
            context.Request.MinimumRevision, ct);
}

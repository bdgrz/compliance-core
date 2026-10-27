using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class GetTenantProvisioningStatusHandler(IAggregateReader reader)
    : IRequestHandler<GetTenantProvisioningStatus, TenantProvisioningStatusView>
{
    public async ValueTask<Result<TenantProvisioningStatusView>> HandleAsync(
        IRequestContext<GetTenantProvisioningStatus> context, CancellationToken ct)
    {
        var tenant = await reader.HydrateAsync(new Tenant(context.Request.TenantId), ct).ConfigureAwait(false);
        return Result<TenantProvisioningStatusView>.Success(tenant.GetProvisioningStatus());
    }
}

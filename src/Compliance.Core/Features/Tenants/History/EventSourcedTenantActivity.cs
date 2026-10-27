using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Features.Tenants.History;

sealed class EventSourcedTenantActivity(IAggregateReader reader) : ITenantActivity
{
    public async ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default)
    {
        var tenant = await reader.HydrateAsync(new Tenant(tenantId), ct).ConfigureAwait(false);
        return tenant.IsActive;
    }
}

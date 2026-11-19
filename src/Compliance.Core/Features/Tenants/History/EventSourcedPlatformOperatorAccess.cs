using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Services;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Tenants.History;

sealed class EventSourcedPlatformOperatorAccess(IAggregateReader reader,
    PlatformOperatorAuthority configuration) : IPlatformOperatorAccess
{
    public async ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default)
    {
        if (userId == Uuid.Empty)
            return false;
        if (configuration.DeveloperAuthentication)
            return true;
        var owner = await reader.HydrateAsync(new PlatformOperator(userId), ct).ConfigureAwait(false);
        return owner.IsActive;
    }
}

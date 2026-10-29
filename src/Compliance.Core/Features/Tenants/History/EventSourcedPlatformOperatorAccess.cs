using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Features.Tenants.History;

sealed class EventSourcedPlatformOperatorAccess(IAggregateReader reader,
    PlatformOperatorAuthority configuration, IImmutableContentStore content) : IPlatformOperatorAccess
{
    public async ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default)
    {
        if (userId == Uuid.Empty)
            return false;
        if (configuration.DeveloperAuthentication)
            return true;
        var roster = await reader.HydrateAsync(new PlatformOperatorRoster(), ct).ConfigureAwait(false);
        var authority = await PlatformOperatorRosterContent.ReadAsync(content, roster, ct).ConfigureAwait(false);
        return authority.Operators.Any(entry => entry.UserId == userId);
    }
}

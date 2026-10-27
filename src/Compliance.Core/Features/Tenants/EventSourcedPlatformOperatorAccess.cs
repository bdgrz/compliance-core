using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

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

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Complete transient affected-episode and live-transition history at one exact native cut.</summary>
public sealed record TenantManagerGuardHistorySnapshot(IReadOnlyDictionary<Uuid, TenantManagerEpisodeAuthority> Episodes,
    IReadOnlyDictionary<Uuid, PendingTransition> Pending, IReadOnlyList<DomainEvent> Facts)
{
    public TenantManagerEpisodeAuthority? EpisodeOf(Uuid episode) => Episodes.GetValueOrDefault(episode);
}

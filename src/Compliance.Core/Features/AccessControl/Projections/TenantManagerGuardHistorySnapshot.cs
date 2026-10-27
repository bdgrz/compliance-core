using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>Complete transient affected-episode and live-transition history at one exact native cut.</summary>
public sealed record TenantManagerGuardHistorySnapshot(IReadOnlyDictionary<Uuid, TenantManagerEpisodeAuthority> Episodes,
    IReadOnlyDictionary<Uuid, PendingTransition> Pending, IReadOnlyList<DomainEvent> Facts)
{
    public TenantManagerEpisodeAuthority? EpisodeOf(Uuid episode) => Episodes.GetValueOrDefault(episode);
}

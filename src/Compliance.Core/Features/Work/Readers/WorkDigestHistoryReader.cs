using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.History;
using Bdgrz.Compliance.Features.Work.Projections;
using Bdgrz.Compliance.Features.Work.Aggregates;
namespace Bdgrz.Compliance.Features.Work.Readers;

/// <summary>Explicit complete technical history and exact current/selected authority.</summary>
public sealed class WorkDigestHistoryReader(IAggregateReader reader, IDomainEventReader events, ISourceFactResolver sources)
{
    public async ValueTask<WorkDigestDispatchHistorySnapshot> ReadDispatchAsync(Uuid tenant, Uuid member,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new WorkDigestDispatch(tenant, member), ct).ConfigureAwait(false);
        return await WorkDigestDispatchHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
    }
    public async ValueTask<WeeklyWorkDigestHistorySnapshot> ReadWeeklyAsync(Uuid tenant, Uuid digest,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new WeeklyWorkDigest(tenant, digest), ct).ConfigureAwait(false);
        return await WeeklyWorkDigestHistory.ReadAsync(events, sources, owner, ct).ConfigureAwait(false);
    }
}

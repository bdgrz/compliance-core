using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Explicit complete technical history and exact current/selected authority.</summary>
public sealed class WorkDigestHistoryReader(IAggregateReader reader, IDomainEventReader events)
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
        return await WeeklyWorkDigestHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
    }
}

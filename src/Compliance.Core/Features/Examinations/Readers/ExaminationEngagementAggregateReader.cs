using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Finalizes actual complete replay authority for queries, executors and exact accepted retry.</summary>
public sealed class ExaminationEngagementAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        var hydrated = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (hydrated is ExaminationEngagement owner)
            _ = await ExaminationEngagementHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        return hydrated;
    }
}

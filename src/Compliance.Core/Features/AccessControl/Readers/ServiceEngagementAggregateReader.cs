using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Finalizes the exact engagement prefix before reads, accepted retries and writes.</summary>
public sealed class ServiceEngagementAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is ServiceEngagementIndependence engagement)
            _ = await ServiceEngagementHistory.ReadAsync(events, engagement, ct).ConfigureAwait(false);
        return owner;
    }
}

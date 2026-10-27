using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Finalizes identical complete gap authority for explicit reads and executor-internal hydration.</summary>
public sealed class ProviderGapAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is ProviderAssuranceGap gap)
            _ = await ProviderGapHistory.ReadAsync(gap, events, ct).ConfigureAwait(false);
        return owner;
    }
}

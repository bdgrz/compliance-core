using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Verifies complete batch prefix before a default read, accepted retry or finalized selected append.</summary>
public sealed class ApplicationImportBatchAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is ApplicationImportBatch batch)
            _ = await ApplicationImportBatchHistory.ReadAsync(events, batch, ct).ConfigureAwait(false);
        return owner;
    }
}

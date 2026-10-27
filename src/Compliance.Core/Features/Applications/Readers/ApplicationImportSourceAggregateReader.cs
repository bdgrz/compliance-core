using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Validates the actual complete source prefix before ordinary reads, accepted retry and selected appends.</summary>
public sealed class ApplicationImportSourceAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is ApplicationImportSource source)
            _ = await ApplicationImportSourceHistory.ReadAsync(events, source, ct).ConfigureAwait(false);
        return owner;
    }
}

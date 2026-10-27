using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Readers;

/// <summary>Finalizes full raw prefix validation for direct reads and executor hydration, without old body reads.</summary>
public sealed class PersonAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default) where TAggregate : Aggregate
    {
        var hydrated = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (hydrated is Person person)
            _ = await PersonHistory.ReadAsync(person, events, ct).ConfigureAwait(false);
        return hydrated;
    }
}

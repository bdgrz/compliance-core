using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings.Readers;

/// <summary>Finalizes whole raw line history for explicit reads and executor-internal rehydration.</summary>
public sealed class MappingLineAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        var hydrated = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (hydrated is ControlCriterionMapping or CriterionApplicability)
            _ = await MappingLineNativeHistory.ReadAsync(hydrated, events, ct).ConfigureAwait(false);
        return hydrated;
    }
}

using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.AuditPopulations.Aggregates;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations.Readers;

/// <summary>Finalizes the complete local/body/source history before either default or executor hydration becomes authoritative.</summary>
public sealed class AuditPopulationAggregateReader(IAggregateReader inner, IDomainEventReader events,
    IImmutableContentStore content, ISourceFactResolver resolver) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default) where TAggregate : Aggregate
    {
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is AuditPopulation population)
            _ = await AuditPopulationHistory.ReadAsync(events, content, resolver, population, ct).ConfigureAwait(false);
        return owner;
    }
}

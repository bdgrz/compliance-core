using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.History;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AuditPopulations.Aggregates;
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

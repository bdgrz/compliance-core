using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.AuditPopulations.Aggregates;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations.Readers;

/// <summary>Composes verified complete views at their exact physical/native owner cut.</summary>
public sealed class AuditPopulationReader(IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content, ISourceFactResolver resolver)
{
    public async Task<AuditPopulationReadSnapshot> ReadAsync(Uuid tenantId, Uuid programId, Uuid populationId,
        CancellationToken ct = default) => await ReadAsync(await reader.HydrateAsync(new AuditPopulation(tenantId,
            programId, populationId), ct).ConfigureAwait(false), ct).ConfigureAwait(false);
    public Task<AuditPopulationReadSnapshot> ReadAsync(AuditPopulation owner, CancellationToken ct = default) =>
        AuditPopulationHistory.ReadAsync(events, content, resolver, owner, ct);
}

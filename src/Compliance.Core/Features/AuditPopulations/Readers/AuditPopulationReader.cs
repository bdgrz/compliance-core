using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPopulations.History;
using Bdgrz.Compliance.Features.AuditPopulations.Projections;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Aggregates;
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

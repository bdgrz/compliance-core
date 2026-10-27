using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Events;
namespace Bdgrz.Compliance.Features.Applications.Projections;

/// <summary>Complete read composition of the batch's facts and current row choices at one certified native cut.</summary>
public sealed class ApplicationImportBatchHistorySnapshot
{
    readonly ApplicationImportBatchPreparedCut _cut;
    readonly Dictionary<Uuid, ApplicationImportRowCorrelated> _correlations;
    internal ApplicationImportBatchHistorySnapshot(ApplicationImportBatch batch, IReadOnlyList<DomainEvent> facts,
        Dictionary<Uuid, ApplicationImportRowCorrelated> correlations)
    {
        _cut = ApplicationImportBatchPreparedCut.Capture(batch);
        Facts = Array.AsReadOnly(facts.ToArray());
        _correlations = new(correlations);
    }
    public IReadOnlyList<DomainEvent> Facts { get; }
    public IReadOnlyCollection<ApplicationImportRowCorrelated> Correlations => _correlations.Values;
    public ApplicationImportRowCorrelated? GetCorrelation(Uuid rowId) => _correlations.GetValueOrDefault(rowId);
    public DomainEvent? FactById(Uuid eventId) => Facts.SingleOrDefault(fact => fact.Metadata.EventId == eventId);
    public bool Matches(ApplicationImportBatch batch) => _cut.Matches(batch);
}

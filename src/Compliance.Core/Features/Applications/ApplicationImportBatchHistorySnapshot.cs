using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

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

using System.Globalization;
using System.Text;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Processes;

/// <summary>Canonical bounded identity for an independently retried publication participant signal.</summary>
static class CriteriaPublicationSignal
{
    internal const string ProcessName = "criteria-publication-signal-v1";
    internal const string Schema = "criteria-publication-signal.v1";
    internal const string Episode = "criteria-publication-signal-v1";

    internal static bool TryAddress(DomainEvent fact, out EventStreamAddress? stream, out Uuid aggregateId)
    {
        var edition = fact switch
        {
            CriteriaPublicationStarted e => e.EditionId,
            CriteriaPublicationAborted e => e.EditionId,
            _ => Uuid.Empty,
        };
        if (edition != Uuid.Empty)
        {
            var owner = new CriteriaEdition(edition);
            stream = owner.Stream;
            aggregateId = owner.Id;
            return true;
        }
        var entry = fact switch
        {
            CriterionWitnessPinned e => (e.EditionId, e.Identifier),
            CriterionWitnessPinFenced e => (e.EditionId, e.Identifier),
            CriterionWitnessRefused e => (e.EditionId, e.Identifier),
            _ => (Uuid.Empty, string.Empty),
        };
        if (entry.Item1 != Uuid.Empty && !string.IsNullOrWhiteSpace(entry.Item2))
        {
            var owner = new CriteriaEntry(entry.Item1, entry.Item2);
            stream = owner.Stream;
            aggregateId = owner.Id;
            return true;
        }
        stream = null;
        aggregateId = Uuid.Empty;
        return false;
    }

    internal static byte[] Payload(EventStreamAddress stream, DomainEvent fact, ulong position) =>
        Encoding.UTF8.GetBytes(string.Join('\n', Schema, fact.GetType().FullName, stream.ToString(),
            fact.Metadata.EventId.ToString(), fact.Metadata.AggregateVersion.ToString(CultureInfo.InvariantCulture),
            position.ToString(CultureInfo.InvariantCulture)));
}

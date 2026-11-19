using System.Globalization;
using System.Text;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Processes;

static class CriteriaEntrySignal
{
    internal const string ProcessName = "criteria-entry-signal-v1";
    internal const string Schema = "criteria-entry-signal.v1";
    internal const string Episode = "criteria-entry-signal-v1";

    internal static CriterionChangeIntent? Intent(DomainEvent fact) => fact switch
    {
        CriterionChangeProposed human => human.Intent,
        CriterionSeedProposed seed => seed.Intent,
        _ => null,
    };

    internal static bool TryAddress(DomainEvent fact, out EventStreamAddress? stream, out Uuid aggregateId)
    {
        var intent = Intent(fact);
        if (intent is not null && CriteriaContentBounds.StructuralShape(intent))
        {
            var owner = new CriteriaEntry(intent.EditionId, intent.Identifier);
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

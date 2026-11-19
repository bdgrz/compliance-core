using System.Globalization;
using System.Text;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Processes;

/// <summary>Canonical bounded identity for one independently retried value proposal.</summary>
static class CriteriaValueSignal
{
    internal const string ProcessName = "criteria-value-signal-v1";
    internal const string Schema = "criteria-value-signal.v1";
    internal const string Episode = "criteria-value-signal-v1";

    internal static bool TryAddress(DomainEvent fact, out EventStreamAddress? stream, out Uuid aggregateId)
    {
        if (fact is CriteriaEditionValueProposed proposal &&
            Bdgrz.Compliance.Features.Criteria.Rules.CriteriaEditionValueRules.Shape(proposal.Intent))
        {
            var i = proposal.Intent;
            var owner = new CriteriaEditionValue(i.EditionId, i.TargetKind, i.Field, i.Category, i.Code);
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

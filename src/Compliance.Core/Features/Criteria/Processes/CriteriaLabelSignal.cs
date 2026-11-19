using System.Globalization;
using System.Text;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Processes;

/// <summary>Canonical identity and bounded immutable intent of a criteria label signal.</summary>
static class CriteriaLabelSignal
{
    internal const string ProcessName = "criteria-label-signal-v1";
    internal const string Schema = "criteria-label-signal.v1";
    internal const string Episode = "criteria-label-signal-v1";

    internal static bool TryAddress(DomainEvent fact, out EventStreamAddress? stream, out Uuid aggregateId)
    {
        var editionId = fact switch
        {
            CriteriaEditionDraftRequested e => e.EditionId,
            CriteriaInitialLabelRequested e => e.EditionId,
            CriteriaEditionRenameRequested e => e.EditionId,
            CriteriaEditionDraftActivated e => e.EditionId,
            CriteriaEditionDraftRenamed e => e.EditionId,
            CriteriaEditionDraftRequestCancelled e => e.EditionId,
            CriteriaEditionRenameCancelled e => e.EditionId,
            CriteriaInitialLabelRefused e => e.EditionId,
            CriteriaEditionRenameRefused e => e.EditionId,
            CriteriaEditionDraftAbandoned e => e.EditionId,
            CriteriaEditionRenameCompleted e => e.EditionId,
            _ => Uuid.Empty,
        };
        if (editionId != Uuid.Empty)
        {
            var owner = new CriteriaEdition(editionId);
            stream = owner.Stream;
            aggregateId = owner.Id;
            return true;
        }
        var label = fact switch
        {
            EditionLabelReserved e => e.Label,
            EditionLabelConfirmed e => e.Label,
            EditionLabelReservationRefused e => e.Label,
            _ => null,
        };
        if (EditionLabelClaim.TryNormalize(label, out var normalized) && normalized == label)
        {
            var owner = new EditionLabelClaim(normalized);
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

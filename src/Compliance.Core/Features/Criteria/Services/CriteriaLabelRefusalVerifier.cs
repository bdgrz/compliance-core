using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Proves a refusal was decided while its exact competing reservation was held.</summary>
sealed class CriteriaLabelRefusalVerifier(IDomainEventReader events, CriteriaLabelSourceVerifier source)
{
    internal async ValueTask<CriteriaLabelRefusalEvidence?> VerifyAsync(string label, Uuid refusalId, CancellationToken ct)
    {
        if (!EditionLabelClaim.TryNormalize(label, out var normalized) || normalized != label || refusalId == Uuid.Empty)
            throw new ArgumentException("The exact normalized claim and refusal identity are required.");
        var claim = new EditionLabelClaim(label);
        EditionLabelReserved? current = null;
        EditionLabelReservationRefused? refusal = null;
        long lastGeneration = 0;
        ulong expected = 0;
        ulong refusalPosition = 0;
        await foreach (var item in events.ReadAsync(claim.Stream, 0, ct).ConfigureAwait(false))
        {
            if (item.Stream != claim.Stream || item.ResourceOffset != expected || item.Event.Metadata.AggregateId != claim.Id || item.Event.Metadata.IsAudit)
                throw new InvalidOperationException("The label refusal source is not a contiguous exact claim history.");
            expected = checked(expected + 1);
            if (item.Event is EditionLabelReserved reserved)
            {
                if (current is not null || lastGeneration == long.MaxValue || reserved.Label != label ||
                    reserved.Generation != lastGeneration + 1 || reserved.Metadata.EventId == Uuid.Empty ||
                    reserved.EditionId == Uuid.Empty || reserved.OperationId == Uuid.Empty ||
                    reserved.EditionRequestEventId == Uuid.Empty)
                    throw new InvalidOperationException("The claim reservation history is inconsistent.");
                current = reserved;
                lastGeneration = reserved.Generation;
            }
            else if (item.Event is EditionLabelReleased released)
            {
                if (current is null || released.Label != label || released.EditionId != current.EditionId ||
                    released.OperationId != current.OperationId || released.Generation != current.Generation)
                    throw new InvalidOperationException("The claim release does not match its live holder.");
                current = null;
            }
            if (item.Event.Metadata.EventId != refusalId)
                continue;
            if (item.Event is not EditionLabelReservationRefused found || current is null || found.Label != label ||
                found.EditionId == Uuid.Empty || found.OperationId == Uuid.Empty || found.EditionRequestEventId == Uuid.Empty ||
                found.RequestedBy is null || string.IsNullOrWhiteSpace(found.RequestedBy.Subject) ||
                string.IsNullOrWhiteSpace(found.RequestedBy.Issuer) || found.HolderEditionId != current.EditionId || found.HolderOperationId != current.OperationId ||
                found.HolderGeneration != current.Generation || found.HolderReservationEventId != current.Metadata.EventId ||
                current.Metadata.AggregateVersion >= found.Metadata.AggregateVersion ||
                found.EditionId == current.EditionId && found.OperationId == current.OperationId ||
                lastGeneration == long.MaxValue || found.RequestedGeneration != lastGeneration + 1)
                throw new InvalidOperationException("The refusal does not name a different live source reservation.");
            refusal = found;
            refusalPosition = item.ResourceOffset;
            break;
        }
        if (refusal is null)
            return null;
        // A selected request has one terminal claim outcome. Verify that no earlier
        // reservation, refusal or fence bound this identity without retaining a roster.
        ulong selectedPosition = 0;
        await foreach (var item in events.ReadAsync(claim.Stream, 0, ct).ConfigureAwait(false))
        {
            if (item.ResourceOffset >= refusalPosition)
                break;
            if (item.Stream != claim.Stream || item.ResourceOffset != selectedPosition ||
                item.Event.Metadata.AggregateId != claim.Id || item.Event.Metadata.IsAudit)
                throw new InvalidOperationException("The selected claim history is not contiguous.");
            selectedPosition = checked(selectedPosition + 1);
            var alreadyBound = item.Event switch
            {
                EditionLabelReserved e => e.EditionId == refusal.EditionId && e.OperationId == refusal.OperationId,
                EditionLabelReservationRefused e => e.EditionId == refusal.EditionId && e.OperationId == refusal.OperationId,
                EditionLabelReservationFenced e => e.EditionId == refusal.EditionId && e.OperationId == refusal.OperationId,
                _ => false,
            };
            if (alreadyBound)
                throw new InvalidOperationException("The refused operation already had a claim outcome or fence.");
        }
        if (selectedPosition != refusalPosition)
            return null;
        var edition = new CriteriaEdition(refusal.EditionId);
        var request = await source.FindAsync<DomainEvent>(edition.Stream, refusal.EditionRequestEventId, ct).ConfigureAwait(false);
        if (request is null)
            return null;
        if (request.Metadata.AggregateId != edition.Id)
            throw new InvalidOperationException("The refused request names another edition owner.");
        var valid = request switch
        {
            CriteriaEditionDraftRequested e => e.EditionId == refusal.EditionId && e.Framework == "tsc" && e.OperationId == refusal.OperationId &&
                e.Label == label && e.Actor == refusal.RequestedBy,
            CriteriaInitialLabelRequested e => e.EditionId == refusal.EditionId && e.OperationId == refusal.OperationId &&
                e.Label == label && e.Actor == refusal.RequestedBy,
            CriteriaEditionRenameRequested e => e.EditionId == refusal.EditionId && e.OperationId == refusal.OperationId &&
                e.NewLabel == label && e.Actor == refusal.RequestedBy,
            _ => false,
        };
        if (!valid)
            throw new InvalidOperationException("The refusal does not bind the exact edition request.");
        return new CriteriaLabelRefusalEvidence(refusal, request);
    }
}

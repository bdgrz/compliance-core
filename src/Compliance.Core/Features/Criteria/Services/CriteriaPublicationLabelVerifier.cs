using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Verifies the exact label request, reservation, edition decision and confirmation chain.</summary>
sealed class CriteriaPublicationLabelVerifier(ICriteriaExactSourceReader source,
    ICriteriaSourceCutCoverage? coverage = null, ICriteriaParticipantCausalOrder? causal = null)
{
    internal async ValueTask<EditionLabelConfirmed?> VerifyAsync(CriteriaPublicationStarted start, ulong startAreaOffset,
        CancellationToken ct)
    {
        if (coverage is null || causal is null || !EditionLabelClaim.TryNormalize(start.Label, out var label) || label != start.Label || start.LabelGeneration <= 0 ||
            start.LabelConfirmationEventId == Uuid.Empty)
            return null;
        var edition = new CriteriaEdition(start.EditionId);
        var claim = new EditionLabelClaim(label);
        var confirmedSource = await source.FindAsync(claim.Stream, claim.Id, start.LabelConfirmationEventId, ct).ConfigureAwait(false);
        if (confirmedSource?.Event is not EditionLabelConfirmed confirmed)
            return null;
        if (confirmed.EditionId != start.EditionId || confirmed.Label != label || confirmed.Generation != start.LabelGeneration ||
            confirmed.OperationId == Uuid.Empty || confirmed.EditionAcceptedEventId == Uuid.Empty)
            throw new InvalidOperationException("The publication label confirmation has another identity.");
        var accepted = await source.FindAsync(edition.Stream, edition.Id, confirmed.EditionAcceptedEventId, ct).ConfigureAwait(false);
        if (accepted is null)
            return null;
        var reservationId = accepted.Event switch
        {
            CriteriaEditionDraftActivated e when e.EditionId == start.EditionId && e.DraftGeneration == start.DraftGeneration &&
                e.OperationId == confirmed.OperationId && e.Label == label && e.LabelGeneration == confirmed.Generation => e.ReservationEventId,
            CriteriaEditionDraftRenamed e when e.EditionId == start.EditionId && e.OperationId == confirmed.OperationId &&
                e.NewLabel == label && e.NewLabelGeneration == confirmed.Generation => e.ReservationEventId,
            _ => Uuid.Empty,
        };
        if (reservationId == Uuid.Empty || accepted.AreaOffset >= startAreaOffset)
            throw new InvalidOperationException("The edition label decision does not precede the publication start.");
        var reservedSource = await source.FindAsync(claim.Stream, claim.Id, reservationId, ct).ConfigureAwait(false);
        if (reservedSource?.Event is not EditionLabelReserved reserved)
            return null;
        if (reserved.EditionId != start.EditionId || reserved.OperationId != confirmed.OperationId || reserved.Label != label ||
            reserved.Generation != confirmed.Generation || reserved.EditionRequestEventId == Uuid.Empty ||
            reservedSource.ResourceOffset >= confirmedSource.ResourceOffset)
            throw new InvalidOperationException("The label reservation and confirmation do not match.");
        var request = await source.FindAsync(edition.Stream, edition.Id, reserved.EditionRequestEventId, ct).ConfigureAwait(false);
        if (request is null)
            return null;
        var matches = (accepted.Event, request.Event) switch
        {
            (CriteriaEditionDraftActivated a, CriteriaEditionDraftRequested e) => e.EditionId == start.EditionId &&
                e.DraftGeneration == start.DraftGeneration && e.Framework == a.Framework && e.OperationId == reserved.OperationId &&
                e.Label == label && e.Actor == reserved.RequestedBy,
            (CriteriaEditionDraftActivated, CriteriaInitialLabelRequested e) => e.EditionId == start.EditionId &&
                e.OperationId == reserved.OperationId && e.Label == label && e.Actor == reserved.RequestedBy,
            (CriteriaEditionDraftRenamed, CriteriaEditionRenameRequested e) => e.EditionId == start.EditionId &&
                e.OperationId == reserved.OperationId && e.NewLabel == label && e.Actor == reserved.RequestedBy,
            _ => false,
        };
        if (!matches || request.AreaOffset >= accepted.AreaOffset)
            throw new InvalidOperationException("The label reservation does not name the exact prior request.");
        if (!await causal.PrecedesAsync(request, reservedSource, ct).ConfigureAwait(false) ||
            !await causal.PrecedesAsync(reservedSource, accepted, ct).ConfigureAwait(false) ||
            !await causal.PrecedesAsync(accepted, confirmedSource, ct).ConfigureAwait(false) ||
            !await new CriteriaValueEditionParticipantVerifier(source, coverage, causal)
                .VerifyConfirmationHistoryAsync(claim, reservedSource, confirmedSource, ct).ConfigureAwait(false))
            return null;
        var participants = new CriteriaValueEditionParticipantVerifier(source, coverage, causal);
        var lifecycle = new CriteriaEntryEditionLifecycle(start.EditionId, start.DraftGeneration);
        var confirmedByEdition = false;
        ulong next = 0;
        await foreach (var item in source.ReadAsync(edition.Stream, edition.Id, ct).ConfigureAwait(false))
        {
            if (item.Stream != edition.Stream || item.Event.Metadata.AggregateId != edition.Id ||
                item.Event.Metadata.IsAudit || item.ResourceOffset != next++)
                return null;
            if (item.AreaOffset >= startAreaOffset)
                continue;
            if (!lifecycle.Apply(item.Event) || !await participants.ObserveAsync(item, ct).ConfigureAwait(false))
                return null;
            if (item.Event is CriteriaEditionDrafted drafted && drafted.EditionId == start.EditionId &&
                drafted.DraftGeneration == start.DraftGeneration && drafted.OperationId == confirmed.OperationId &&
                drafted.Label == label && drafted.LabelGeneration == confirmed.Generation && drafted.ConfirmationEventId == confirmed.Metadata.EventId &&
                await causal.PrecedesAsync(confirmedSource, item, ct).ConfigureAwait(false))
                confirmedByEdition = true;
            if (item.Event is CriteriaEditionRenameCompleted renamed && renamed.EditionId == start.EditionId &&
                renamed.OperationId == confirmed.OperationId && renamed.Label == label && renamed.LabelGeneration == confirmed.Generation &&
                renamed.ConfirmationEventId == confirmed.Metadata.EventId &&
                await causal.PrecedesAsync(confirmedSource, item, ct).ConfigureAwait(false))
                confirmedByEdition = true;
        }
        return confirmedByEdition && await coverage.CoversAsync(edition.Stream, next, startAreaOffset, ct).ConfigureAwait(false)
            ? confirmed : null;
    }
}

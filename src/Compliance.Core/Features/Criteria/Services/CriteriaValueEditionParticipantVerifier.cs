using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Handlers;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Checks independently owned label and witness outcomes in one edition history.</summary>
/// <remarks>A missing or unqualified participant fact is pending, never evidence of success.</remarks>
sealed class CriteriaValueEditionParticipantVerifier(ICriteriaExactSourceReader source,
    ICriteriaSourceCutCoverage coverage, ICriteriaParticipantCausalOrder? causalOrder = null)
{
    CriteriaOrderedSourceEvent? _initialRequest;
    CriteriaOrderedSourceEvent? _renameRequest;
    CriteriaOrderedSourceEvent? _renamed;
    CriteriaOrderedSourceEvent? _aborted;

    public async ValueTask<bool> ObserveAsync(CriteriaOrderedSourceEvent record, CancellationToken ct)
    {
        switch (record.Event)
        {
            case CriteriaEditionDraftRequested or CriteriaInitialLabelRequested:
                _initialRequest = record;
                return true;
            case CriteriaInitialLabelRefused refused:
                return _initialRequest is not null && refused.Reason == ApplyCriteriaEditionLabelRefusalHandler.Reason &&
                    await VerifyRefusalAsync(_initialRequest, refused.EditionId, refused.OperationId,
                        refused.Label, refused.RefusalEventId, record, ct).ConfigureAwait(false);
            case CriteriaEditionRenameRequested:
                _renameRequest = record;
                _renamed = null;
                return true;
            case CriteriaEditionDraftRenamed renamed:
                if (_renameRequest?.Event is not CriteriaEditionRenameRequested request ||
                    _renameRequest.AreaOffset >= record.AreaOffset || request.EditionId != renamed.EditionId ||
                    request.OperationId != renamed.OperationId || request.NewLabel != renamed.NewLabel ||
                    request.OldLabel != renamed.OldLabel || request.OldLabelGeneration != renamed.OldLabelGeneration)
                    return false;
                var reservation = await FindReservationAsync(request, renamed.ReservationEventId, ct).ConfigureAwait(false);
                if (reservation is null || reservation.Event is not EditionLabelReserved held || held.Generation != renamed.NewLabelGeneration ||
                    !await PrecedesAsync(_renameRequest!, reservation, ct).ConfigureAwait(false) ||
                    !await PrecedesAsync(reservation, record, ct).ConfigureAwait(false))
                    return false;
                _renamed = record;
                return true;
            case CriteriaEditionRenameCompleted completed:
                if (_renameRequest?.Event is not CriteriaEditionRenameRequested intent ||
                    _renamed?.Event is not CriteriaEditionDraftRenamed committed || _renamed.AreaOffset >= record.AreaOffset ||
                    completed.EditionId != intent.EditionId || completed.OperationId != intent.OperationId ||
                    completed.Label != intent.NewLabel || completed.LabelGeneration != committed.NewLabelGeneration)
                    return false;
                var reserved = await FindReservationAsync(intent, committed.ReservationEventId, ct).ConfigureAwait(false);
                if (reserved is null)
                    return false;
                var claim = new EditionLabelClaim(completed.Label);
                var confirmation = await source.FindAsync(claim.Stream, claim.Id, completed.ConfirmationEventId, ct).ConfigureAwait(false);
                if (confirmation?.Event is not EditionLabelConfirmed confirmed || !Exact(confirmation, claim.Stream, claim.Id) ||
                    confirmed.Metadata.EventId != completed.ConfirmationEventId ||
                    confirmation.ResourceOffset <= reserved.ResourceOffset || confirmed.EditionId != completed.EditionId ||
                    confirmed.OperationId != completed.OperationId || confirmed.Label != completed.Label ||
                    confirmed.Generation != completed.LabelGeneration || confirmed.EditionAcceptedEventId != committed.Metadata.EventId ||
                    !await VerifyConfirmationHistoryAsync(claim, reserved, confirmation, ct).ConfigureAwait(false) ||
                    !await PrecedesAsync(_renamed!, confirmation, ct).ConfigureAwait(false) ||
                    !await PrecedesAsync(confirmation, record, ct).ConfigureAwait(false))
                    return false;
                _renameRequest = _renamed = null;
                return true;
            case CriteriaEditionRenameRefused renameRefusal:
                if (_renameRequest?.Event is not CriteriaEditionRenameRequested failed ||
                    renameRefusal.Reason != ApplyCriteriaEditionLabelRefusalHandler.Reason ||
                    !await VerifyRefusalAsync(_renameRequest, renameRefusal.EditionId, renameRefusal.OperationId,
                        failed.NewLabel, renameRefusal.RefusalEventId, record, ct).ConfigureAwait(false))
                    return false;
                _renameRequest = _renamed = null;
                return true;
            case CriteriaEditionRenameCancelled:
                _renameRequest = _renamed = null;
                return true;
            case CriteriaPublicationAborted:
                _aborted = record;
                return true;
            case CriteriaPublicationAbortCleanupCompleted cleanup:
                if (_aborted?.Event is not CriteriaPublicationAborted aborted || _aborted.AreaOffset >= record.AreaOffset ||
                    aborted.EditionId != cleanup.EditionId || aborted.DraftGeneration != cleanup.DraftGeneration ||
                    aborted.PublicationAttemptId != cleanup.PublicationAttemptId || aborted.AttemptOrdinal != cleanup.AttemptOrdinal ||
                    aborted.WitnessIdentifier != cleanup.WitnessIdentifier || aborted.WitnessIncarnationId != cleanup.WitnessIncarnationId)
                    return false;
                if (!CriteriaContentBounds.Identifier(cleanup.WitnessIdentifier))
                    return false;
                var witness = new CriteriaEntry(cleanup.EditionId, cleanup.WitnessIdentifier);
                var fence = await source.FindAsync(witness.Stream, witness.Id, cleanup.WitnessFenceEventId, ct).ConfigureAwait(false);
                if (fence?.Event is not CriterionWitnessPinFenced pinned || !Exact(fence, witness.Stream, witness.Id) ||
                    pinned.Metadata.EventId != cleanup.WitnessFenceEventId ||
                    _aborted.AreaOffset >= fence.AreaOffset || fence.AreaOffset >= record.AreaOffset ||
                    pinned.EditionId != cleanup.EditionId || pinned.DraftGeneration != cleanup.DraftGeneration ||
                    pinned.Identifier != cleanup.WitnessIdentifier || pinned.PublicationAttemptId != cleanup.PublicationAttemptId ||
                    pinned.AttemptOrdinal != cleanup.AttemptOrdinal || pinned.IncarnationId != cleanup.WitnessIncarnationId ||
                    pinned.PublicationAbortedEventId != aborted.Metadata.EventId)
                    return false;
                _aborted = null;
                return true;
            default:
                return true;
        }
    }

    async ValueTask<CriteriaOrderedSourceEvent?> FindReservationAsync(CriteriaEditionRenameRequested request,
        Uuid reservationId, CancellationToken ct)
    {
        if (reservationId == Uuid.Empty)
            return null;
        var owner = new EditionLabelClaim(request.NewLabel);
        var found = await source.FindAsync(owner.Stream, owner.Id, reservationId, ct).ConfigureAwait(false);
        return found?.Event is EditionLabelReserved reserved && Exact(found, owner.Stream, owner.Id) &&
            reserved.Metadata.EventId == reservationId &&
            reserved.Label == request.NewLabel && reserved.EditionId == request.EditionId &&
            reserved.OperationId == request.OperationId && reserved.EditionRequestEventId == request.Metadata.EventId &&
            reserved.RequestedBy == request.Actor && reserved.Generation > 0 ? found : null;
    }

    async ValueTask<bool> VerifyRefusalAsync(CriteriaOrderedSourceEvent request, Uuid editionId, Uuid operationId,
        string label, Uuid refusalId, CriteriaOrderedSourceEvent outcome, CancellationToken ct)
    {
        if (refusalId == Uuid.Empty || !EditionLabelClaim.TryNormalize(label, out var normalized) || normalized != label)
            return false;
        var claim = new EditionLabelClaim(label);
        EditionLabelReserved? holder = null;
        long generation = 0;
        ulong next = 0;
        await foreach (var item in source.ReadAsync(claim.Stream, claim.Id, ct).ConfigureAwait(false))
        {
            if (!Exact(item, claim.Stream, claim.Id) || item.ResourceOffset != next++)
                throw new InvalidOperationException("The label participant source is not contiguous.");
            if (item.Event.Metadata.EventId == refusalId)
            {
                if (item.Event is not EditionLabelReservationRefused refused || holder is null ||
                    refused.Label != label || refused.EditionId != editionId || refused.OperationId != operationId ||
                    refused.EditionRequestEventId != request.Event.Metadata.EventId ||
                    refused.RequestedBy != RequestActorOf(request.Event) || generation == long.MaxValue ||
                    refused.RequestedGeneration != generation + 1 || refused.HolderEditionId != holder.EditionId ||
                    refused.HolderOperationId != holder.OperationId || refused.HolderGeneration != holder.Generation ||
                    refused.HolderReservationEventId != holder.Metadata.EventId ||
                    (holder.EditionId == editionId && holder.OperationId == operationId))
                    return false;
                return await coverage.CoversAsync(claim.Stream, next, item.AreaOffset, ct).ConfigureAwait(false) &&
                    await PrecedesAsync(request, item, ct).ConfigureAwait(false) &&
                    await PrecedesAsync(item, outcome, ct).ConfigureAwait(false);
            }
            switch (item.Event)
            {
                case EditionLabelReserved reserved:
                    if (holder is not null || generation == long.MaxValue || reserved.Label != label ||
                        reserved.Generation != generation + 1 || reserved.Metadata.EventId == Uuid.Empty ||
                        reserved.EditionId == Uuid.Empty || reserved.OperationId == Uuid.Empty || reserved.EditionRequestEventId == Uuid.Empty ||
                        reserved.EditionId == editionId && reserved.OperationId == operationId)
                        return false;
                    holder = reserved;
                    generation = reserved.Generation;
                    break;
                case EditionLabelReleased released:
                    if (holder is null || released.Label != label || released.EditionId != holder.EditionId ||
                        released.OperationId != holder.OperationId || released.Generation != holder.Generation)
                        return false;
                    holder = null;
                    break;
                case EditionLabelConfirmed confirmed:
                    if (holder is null || confirmed.Label != label || confirmed.EditionId != holder.EditionId ||
                        confirmed.OperationId != holder.OperationId || confirmed.Generation != holder.Generation ||
                        confirmed.EditionAcceptedEventId == Uuid.Empty)
                        return false;
                    break;
                case EditionLabelReservationRefused earlier when earlier.EditionId == editionId && earlier.OperationId == operationId:
                case EditionLabelReservationFenced fenced when fenced.EditionId == editionId && fenced.OperationId == operationId:
                    return false;
            }
        }
        return false;
    }

    internal async ValueTask<bool> VerifyConfirmationHistoryAsync(EditionLabelClaim owner, CriteriaOrderedSourceEvent reservation,
        CriteriaOrderedSourceEvent confirmation, CancellationToken ct)
    {
        if (reservation.Event is not EditionLabelReserved expected || confirmation.Event is not EditionLabelConfirmed expectedConfirmation)
            return false;
        EditionLabelReserved? holder = null;
        long generation = 0;
        ulong next = 0;
        await foreach (var item in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            if (!Exact(item, owner.Stream, owner.Id) || item.ResourceOffset != next++)
                throw new InvalidOperationException("The label participant source is not contiguous.");
            if (item.Event.Metadata.EventId == expectedConfirmation.Metadata.EventId)
            {
                if (item.ResourceOffset != confirmation.ResourceOffset || item.Event != expectedConfirmation ||
                    holder is null || holder.Metadata.EventId != expected.Metadata.EventId)
                    return false;
                return await coverage.CoversAsync(owner.Stream, next, item.AreaOffset, ct).ConfigureAwait(false);
            }
            switch (item.Event)
            {
                case EditionLabelReserved held:
                    if (holder is not null || generation == long.MaxValue || held.Label != owner.Label ||
                        held.Generation != generation + 1 || held.EditionId == Uuid.Empty || held.OperationId == Uuid.Empty ||
                        held.EditionRequestEventId == Uuid.Empty)
                        return false;
                    if (held.Metadata.EventId == expected.Metadata.EventId &&
                        (item.ResourceOffset != reservation.ResourceOffset || held != expected))
                        return false;
                    holder = held;
                    generation = held.Generation;
                    break;
                case EditionLabelReleased released:
                    if (holder is null || released.Label != owner.Label || released.EditionId != holder.EditionId ||
                        released.OperationId != holder.OperationId || released.Generation != holder.Generation)
                        return false;
                    holder = null;
                    break;
                case EditionLabelConfirmed confirmed:
                    if (holder is null || confirmed.Label != owner.Label || confirmed.EditionId != holder.EditionId ||
                        confirmed.OperationId != holder.OperationId || confirmed.Generation != holder.Generation ||
                        confirmed.EditionAcceptedEventId == Uuid.Empty ||
                        confirmed.EditionId == expected.EditionId && confirmed.OperationId == expected.OperationId)
                        return false;
                    break;
                case EditionLabelReservationRefused prior when prior.EditionId == expected.EditionId && prior.OperationId == expected.OperationId:
                case EditionLabelReservationFenced priorFence when priorFence.EditionId == expected.EditionId && priorFence.OperationId == expected.OperationId:
                    return false;
            }
        }
        return false;
    }

    static ActorAttribution? RequestActorOf(DomainEvent value) => value switch
    {
        CriteriaEditionDraftRequested e => e.Actor,
        CriteriaInitialLabelRequested e => e.Actor,
        CriteriaEditionRenameRequested e => e.Actor,
        _ => null,
    };

    ValueTask<bool> PrecedesAsync(CriteriaOrderedSourceEvent first, CriteriaOrderedSourceEvent second, CancellationToken ct) =>
        causalOrder is null ? ValueTask.FromResult(false) : causalOrder.PrecedesAsync(first, second, ct);

    static bool Exact(CriteriaOrderedSourceEvent item, EventStreamAddress stream, Uuid id) =>
        item.Stream == stream && item.Event.Metadata.AggregateId == id && !item.Event.Metadata.IsAudit &&
        item.Event.Metadata.EventId != Uuid.Empty && item.Event.Metadata.AggregateVersion == checked(item.ResourceOffset + 1);
}

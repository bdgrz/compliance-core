using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Confirms a label only from the exact committed edition and reservation facts.</summary>
sealed class ConfirmCriteriaEditionLabelHandler(
    IAggregateExecutor executor,
    CriteriaLabelSourceVerifier source) : IRequestHandler<ConfirmCriteriaEditionLabel>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ConfirmCriteriaEditionLabel> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "Only the edition label reaction can confirm a claim.");

        var request = context.Request;
        if (request.EditionId == Uuid.Empty || request.OperationId == Uuid.Empty || request.Generation <= 0 ||
            request.ReservationEventId == Uuid.Empty || request.EditionEventId == Uuid.Empty ||
            !EditionLabelClaim.TryNormalize(request.Label, out var label) || label != request.Label)
            return Failure(RequestErrorKind.Validation, "The exact edition, label operation and source facts are required.");

        var editionEvent = await source.ReadAsync<DomainEvent>(CriteriaEdition.AddressOf(request.EditionId),
            request.EditionEventPosition, request.EditionEventId, ct).ConfigureAwait(false);
        if (editionEvent is null)
            return Failure(RequestErrorKind.Conflict, "The committed edition decision is not yet observable.");
        if (editionEvent.Metadata.AggregateId != request.EditionId)
            return Failure(RequestErrorKind.Validation, "The edition decision belongs to another edition.");

        var matchesEdition = editionEvent switch
        {
            CriteriaEditionDraftActivated activated => activated.EditionId == request.EditionId && activated.Framework == "tsc" &&
                activated.OperationId == request.OperationId && activated.Label == label &&
                activated.LabelGeneration == request.Generation && activated.ReservationEventId == request.ReservationEventId,
            CriteriaEditionDraftRenamed renamed => renamed.EditionId == request.EditionId &&
                renamed.OperationId == request.OperationId && renamed.NewLabel == label &&
                renamed.NewLabelGeneration == request.Generation && renamed.ReservationEventId == request.ReservationEventId,
            _ => false,
        };
        if (!matchesEdition)
            return Failure(RequestErrorKind.Validation, "The source event does not authorize this label confirmation.");

        var reservation = await source.FindAsync<EditionLabelReserved>(EditionLabelClaim.AddressOf(label),
            request.ReservationEventId, ct).ConfigureAwait(false);
        if (reservation is null)
            return Failure(RequestErrorKind.Conflict, "The exact committed label reservation is not yet observable.");
        if (reservation.Metadata.AggregateId != new EditionLabelClaim(label).Id ||
            reservation.EditionId != request.EditionId || reservation.OperationId != request.OperationId ||
            reservation.Label != label || reservation.Generation != request.Generation)
            return Failure(RequestErrorKind.Validation, "The source reservation names another label operation.");

        var intent = await source.FindAsync<DomainEvent>(CriteriaEdition.AddressOf(request.EditionId),
            reservation.EditionRequestEventId, ct).ConfigureAwait(false);
        if (intent is null)
            return Failure(RequestErrorKind.Conflict, "The edition request named by the reservation is not yet observable.");
        if (intent.Metadata.AggregateId != request.EditionId)
            return Failure(RequestErrorKind.Validation, "The edition request belongs to another edition.");

        var matchesIntent = (editionEvent, intent) switch
        {
            (CriteriaEditionDraftActivated, CriteriaEditionDraftRequested draft) =>
                draft.EditionId == request.EditionId && draft.Framework == "tsc" && draft.OperationId == request.OperationId &&
                draft.Label == label && draft.Actor == reservation.RequestedBy,
            (CriteriaEditionDraftActivated, CriteriaInitialLabelRequested initial) =>
                initial.EditionId == request.EditionId && initial.OperationId == request.OperationId &&
                initial.Label == label && initial.Actor == reservation.RequestedBy,
            (CriteriaEditionDraftRenamed, CriteriaEditionRenameRequested rename) =>
                rename.EditionId == request.EditionId && rename.OperationId == request.OperationId &&
                rename.NewLabel == label && rename.Actor == reservation.RequestedBy,
            _ => false,
        };
        if (!matchesIntent)
            return Failure(RequestErrorKind.Validation, "The reservation does not name the exact edition request.");
        if (!CriteriaParticipantAdmission.AllowsLabel(intent))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The legacy request requires qualified recovery.", true));

        return await executor.ExecuteAsync(new EditionLabelClaim(label, request.EditionId, request.OperationId), claim =>
        {
            // A released exact reservation is terminal. Its earlier confirmation must not reacquire it.
            if (claim.ReservationStatus(request.EditionId, request.OperationId) == EditionLabelReservationStatus.Released)
                return AggregateOutcome.Discard(Result.Success);

            if (!claim.IsHeldBy(request.EditionId, request.OperationId, request.Generation) ||
                claim.CurrentReservation?.Metadata.EventId != request.ReservationEventId)
                return AggregateOutcome.Discard(Failure(RequestErrorKind.Conflict,
                    "The exact label reservation is not currently observable."));

            return AggregateOutcome.CommitOnSuccess(claim.Confirm(request.EditionId, request.OperationId,
                request.Generation, request.EditionEventId));
        }, context, ct).ConfigureAwait(false);
    }

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}

using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Completes the exact edition transition after its label confirmation.</summary>
sealed class ApplyCriteriaLabelConfirmationHandler(IAggregateExecutor executor,
    CriteriaLabelSourceVerifier source) : IRequestHandler<ApplyCriteriaLabelConfirmation>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApplyCriteriaLabelConfirmation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A system reaction is required.");
        if (request.EditionId == Uuid.Empty || request.OperationId == Uuid.Empty || request.SourceEventId == Uuid.Empty ||
            !EditionLabelClaim.TryNormalize(request.Label, out var label) || label != request.Label)
            return Failure(RequestErrorKind.Validation, "The exact label confirmation is required.");
        var confirmation = await source.ReadAsync<EditionLabelConfirmed>(EditionLabelClaim.AddressOf(label),
            request.SourcePosition, request.SourceEventId, ct).ConfigureAwait(false);
        if (confirmation is null)
            return Failure(RequestErrorKind.Conflict, "The label confirmation is not yet observable.");
        if (confirmation.Metadata.AggregateId != new EditionLabelClaim(label).Id ||
            confirmation.EditionId != request.EditionId || confirmation.OperationId != request.OperationId ||
            confirmation.Label != label || confirmation.Generation <= 0 || confirmation.EditionAcceptedEventId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "The confirmation names another edition transition.");
        var accepted = await source.FindAsync<DomainEvent>(CriteriaEdition.AddressOf(request.EditionId),
            confirmation.EditionAcceptedEventId, ct).ConfigureAwait(false);
        if (accepted is null)
            return Failure(RequestErrorKind.Conflict, "The edition decision is not yet observable.");
        if (accepted.Metadata.AggregateId != request.EditionId)
            return Failure(RequestErrorKind.Validation, "The edition decision belongs to another edition.");
        var initial = accepted switch
        {
            CriteriaEditionDraftActivated draft when draft.EditionId == request.EditionId && draft.Framework == "tsc" && draft.OperationId == request.OperationId &&
                draft.Label == label && draft.LabelGeneration == confirmation.Generation => true,
            CriteriaEditionDraftRenamed rename when rename.EditionId == request.EditionId && rename.OperationId == request.OperationId &&
                rename.NewLabel == label && rename.NewLabelGeneration == confirmation.Generation => false,
            _ => (bool?)null,
        };
        if (initial is null)
            return Failure(RequestErrorKind.Validation, "The confirmation does not name the edition decision.");
        var reservationId = accepted switch
        {
            CriteriaEditionDraftActivated draft => draft.ReservationEventId,
            CriteriaEditionDraftRenamed rename => rename.ReservationEventId,
            _ => Uuid.Empty,
        };
        var reservation = await source.FindAsync<EditionLabelReserved>(EditionLabelClaim.AddressOf(label), reservationId, ct).ConfigureAwait(false);
        if (reservation is null || reservation.Metadata.AggregateId != new EditionLabelClaim(label).Id ||
            reservation.EditionId != request.EditionId || reservation.OperationId != request.OperationId ||
            reservation.Generation != confirmation.Generation || reservation.Label != label)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The exact reservation is unavailable.", true));
        var intent = await source.FindAsync<DomainEvent>(CriteriaEdition.AddressOf(request.EditionId), reservation.EditionRequestEventId, ct).ConfigureAwait(false);
        var exactIntent = intent switch
        {
            CriteriaEditionDraftRequested draft => initial.Value && draft.EditionId == request.EditionId && draft.OperationId == request.OperationId && draft.Label == label && draft.Actor == reservation.RequestedBy,
            CriteriaInitialLabelRequested choose => initial.Value && choose.EditionId == request.EditionId && choose.OperationId == request.OperationId && choose.Label == label && choose.Actor == reservation.RequestedBy,
            CriteriaEditionRenameRequested rename => !initial.Value && rename.EditionId == request.EditionId && rename.OperationId == request.OperationId && rename.NewLabel == label && rename.Actor == reservation.RequestedBy,
            _ => false,
        };
        if (!exactIntent || intent is null || intent.Metadata.AggregateId != request.EditionId || !CriteriaParticipantAdmission.AllowsLabel(intent))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The original admission requires qualified recovery.", true));
        return await executor.ExecuteAsync(new CriteriaEdition(request.EditionId, request.OperationId), owner =>
            AggregateOutcome.CommitOnSuccess(initial.Value
                ? owner.CompleteDraft(request.OperationId, confirmation.Generation, request.SourceEventId)
                : owner.CompleteRename(request.OperationId, request.SourceEventId)), context, ct).ConfigureAwait(false);
    }

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}

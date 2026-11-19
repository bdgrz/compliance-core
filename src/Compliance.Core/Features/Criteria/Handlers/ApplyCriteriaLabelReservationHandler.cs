using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Applies a label reservation only to the exact edition request that caused it.</summary>
sealed class ApplyCriteriaLabelReservationHandler(IAggregateExecutor executor,
    CriteriaLabelSourceVerifier source) : IRequestHandler<ApplyCriteriaLabelReservation>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApplyCriteriaLabelReservation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A system reaction is required.");
        if (request.EditionId == Uuid.Empty || request.OperationId == Uuid.Empty || request.SourceEventId == Uuid.Empty ||
            !EditionLabelClaim.TryNormalize(request.Label, out var label) || label != request.Label)
            return Failure(RequestErrorKind.Validation, "The exact label reservation is required.");
        var reservation = await source.ReadAsync<EditionLabelReserved>(EditionLabelClaim.AddressOf(label),
            request.SourcePosition, request.SourceEventId, ct).ConfigureAwait(false);
        if (reservation is null)
            return Failure(RequestErrorKind.Conflict, "The label reservation is not yet observable.");
        if (reservation.Metadata.AggregateId != new EditionLabelClaim(label).Id ||
            reservation.EditionId != request.EditionId || reservation.OperationId != request.OperationId ||
            reservation.Label != label || reservation.Generation <= 0 || reservation.EditionRequestEventId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "The reservation names another edition request.");
        var intent = await source.FindAsync<DomainEvent>(CriteriaEdition.AddressOf(request.EditionId),
            reservation.EditionRequestEventId, ct).ConfigureAwait(false);
        if (intent is null)
            return Failure(RequestErrorKind.Conflict, "The edition request is not yet observable.");
        if (intent.Metadata.AggregateId != request.EditionId)
            return Failure(RequestErrorKind.Validation, "The edition request belongs to another edition.");
        var initial = intent switch
        {
            CriteriaEditionDraftRequested draft when draft.EditionId == request.EditionId && draft.Framework == "tsc" && draft.OperationId == request.OperationId &&
                draft.Label == label && draft.Actor == reservation.RequestedBy => true,
            CriteriaInitialLabelRequested choose when choose.EditionId == request.EditionId && choose.OperationId == request.OperationId &&
                choose.Label == label && choose.Actor == reservation.RequestedBy => true,
            CriteriaEditionRenameRequested rename when rename.EditionId == request.EditionId && rename.OperationId == request.OperationId &&
                rename.NewLabel == label && rename.Actor == reservation.RequestedBy => false,
            _ => (bool?)null,
        };
        if (initial is null)
            return Failure(RequestErrorKind.Validation, "The reservation has no matching edition request.");
        if (!CriteriaParticipantAdmission.AllowsLabel(intent))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The legacy request requires qualified recovery.", true));
        return await executor.ExecuteAsync(new CriteriaEdition(request.EditionId, request.OperationId), owner =>
            AggregateOutcome.CommitOnSuccess(initial.Value
                ? owner.ActivateDraft(request.OperationId, reservation.Generation, request.SourceEventId)
                : owner.CommitRename(request.OperationId, reservation.Generation, request.SourceEventId)), context, ct).ConfigureAwait(false);
    }

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}

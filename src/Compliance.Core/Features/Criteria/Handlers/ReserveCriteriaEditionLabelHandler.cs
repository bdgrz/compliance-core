using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Reserves a label using only the original committed edition intent.</summary>
sealed class ReserveCriteriaEditionLabelHandler(IAggregateExecutor executor,
    CriteriaLabelSourceVerifier source) : IRequestHandler<ReserveCriteriaEditionLabel>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReserveCriteriaEditionLabel> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "Only the edition label reaction can reserve a claim.");

        var request = context.Request;
        if (request.EditionId == Uuid.Empty || request.OperationId == Uuid.Empty ||
            request.EditionEventId == Uuid.Empty || !EditionLabelClaim.TryNormalize(request.Label, out var label) ||
            label != request.Label)
            return Failure(RequestErrorKind.Validation, "The exact edition request and normalized label are required.");

        var intent = await source.ReadAsync<DomainEvent>(CriteriaEdition.AddressOf(request.EditionId),
            request.EditionEventPosition, request.EditionEventId, ct).ConfigureAwait(false);
        if (intent is null)
            return Failure(RequestErrorKind.Conflict, "The committed edition request is not yet observable.");
        if (intent.Metadata.AggregateId != request.EditionId)
            return Failure(RequestErrorKind.Validation, "The committed request belongs to another edition.");

        var actor = intent switch
        {
            CriteriaEditionDraftRequested draft when draft.EditionId == request.EditionId && draft.Framework == "tsc" &&
                draft.OperationId == request.OperationId && draft.Label == label => draft.Actor,
            CriteriaInitialLabelRequested initial when initial.EditionId == request.EditionId &&
                initial.OperationId == request.OperationId && initial.Label == label => initial.Actor,
            CriteriaEditionRenameRequested rename when rename.EditionId == request.EditionId &&
                rename.OperationId == request.OperationId && rename.NewLabel == label => rename.Actor,
            _ => null,
        };
        if (actor is null)
            return Failure(RequestErrorKind.Validation, "The source fact does not authorize this label request.");
        if (!CriteriaParticipantAdmission.AllowsLabel(intent))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The legacy request requires qualified recovery.", true));

        return await executor.ExecuteAsync(new EditionLabelClaim(label, request.EditionId, request.OperationId), claim =>
        {
            if (claim.IsRequestFenced(request.EditionId, request.OperationId, request.EditionEventId))
                return AggregateOutcome.Discard(Result.Success);
            var previous = claim.ReservationFor(request.EditionId, request.OperationId);
            if (previous is null && claim.LastGeneration == long.MaxValue)
                return AggregateOutcome.Discard(Failure(RequestErrorKind.Conflict, "The label generation is exhausted."));
            var generation = previous?.Generation ?? claim.LastGeneration + 1;
            var outcome = claim.Reserve(request.EditionId, request.OperationId, generation,
                request.EditionEventId, actor);
            // A conflict is not a durable refusal: its source-current authority is unqualified.
            return outcome.IsSuccess ? AggregateOutcome.Commit(Result.Success) :
                AggregateOutcome.Discard(Result.Failure(outcome.Error!));
        }, context, ct).ConfigureAwait(false);
    }

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}

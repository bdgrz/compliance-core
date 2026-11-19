using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Releases only a claim proven terminal by its exact edition source facts.</summary>
sealed class ReleaseCriteriaEditionLabelHandler(IAggregateExecutor executor,
    CriteriaLabelSourceVerifier source) : IRequestHandler<ReleaseCriteriaEditionLabel>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReleaseCriteriaEditionLabel> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "Only a system reaction can release a criteria label.");
        var request = context.Request;
        if (request.EditionId == Uuid.Empty || request.ClaimOperationId == Uuid.Empty ||
            request.Generation <= 0 || request.ReservationEventId == Uuid.Empty || request.EditionEventId == Uuid.Empty ||
            !EditionLabelClaim.TryNormalize(request.Label, out var label) || request.Label != label)
            return Failure(RequestErrorKind.Validation, "The exact edition, label claim and source facts are required.");

        var claim = new EditionLabelClaim(label, request.EditionId, request.ClaimOperationId);
        var reservation = await source.FindAsync<EditionLabelReserved>(claim.Stream, request.ReservationEventId, ct)
            .ConfigureAwait(false);
        if (reservation is null)
            return Pending();
        if (reservation.Metadata.AggregateId != claim.Id || reservation.EditionId != request.EditionId ||
            reservation.OperationId != request.ClaimOperationId || reservation.Label != label ||
            reservation.Generation != request.Generation)
            return Failure(RequestErrorKind.Validation, "The reservation names a different label claim.");

        var edition = new CriteriaEdition(request.EditionId);
        var intent = await source.FindAsync<DomainEvent>(edition.Stream, reservation.EditionRequestEventId, ct)
            .ConfigureAwait(false);
        var terminal = await source.ReadAsync<DomainEvent>(edition.Stream, request.EditionEventPosition,
            request.EditionEventId, ct).ConfigureAwait(false);
        if (intent is null || terminal is null)
            return Pending();
        if (intent.Metadata.AggregateId != edition.Id || terminal.Metadata.AggregateId != edition.Id)
            return Failure(RequestErrorKind.Validation, "The edition evidence names a different aggregate.");
        if (!CriteriaParticipantAdmission.AllowsLabel(intent))
            return Pending();

        CriteriaEditionDraftRenamed? renamed = null;
        EditionLabelConfirmed? replacement = null;
        EditionLabelReserved? replacementReservation = null;
        CriteriaEditionRenameRequested? renameIntent = null;
        if (terminal is CriteriaEditionRenameCompleted completed)
        {
            if (!EditionLabelClaim.TryNormalize(completed.Label, out var replacementLabel) ||
                replacementLabel != completed.Label || completed.ConfirmationEventId == Uuid.Empty)
                return Failure(RequestErrorKind.Validation, "The replacement confirmation is invalid.");
            var replacementClaim = new EditionLabelClaim(replacementLabel);
            replacement = await source.FindAsync<EditionLabelConfirmed>(replacementClaim.Stream,
                completed.ConfirmationEventId, ct).ConfigureAwait(false);
            if (replacement is null)
                return Pending();
            if (replacement.Metadata.AggregateId != replacementClaim.Id || replacement.EditionAcceptedEventId == Uuid.Empty)
                return Failure(RequestErrorKind.Validation, "The replacement confirmation names another claim.");
            renamed = await source.FindAsync<CriteriaEditionDraftRenamed>(edition.Stream,
                replacement.EditionAcceptedEventId, ct).ConfigureAwait(false);
            if (renamed is null)
                return Pending();
            if (renamed.Metadata.AggregateId != edition.Id || renamed.ReservationEventId == Uuid.Empty)
                return Failure(RequestErrorKind.Validation, "The rename decision names another edition.");
            replacementReservation = await source.FindAsync<EditionLabelReserved>(replacementClaim.Stream,
                renamed.ReservationEventId, ct).ConfigureAwait(false);
            if (replacementReservation is null)
                return Pending();
            if (replacementReservation.Metadata.AggregateId != replacementClaim.Id ||
                replacementReservation.EditionRequestEventId == Uuid.Empty)
                return Failure(RequestErrorKind.Validation, "The replacement reservation names another claim.");
            renameIntent = await source.FindAsync<CriteriaEditionRenameRequested>(edition.Stream,
                replacementReservation.EditionRequestEventId, ct).ConfigureAwait(false);
            if (renameIntent is null)
                return Pending();
            if (renameIntent.Metadata.AggregateId != edition.Id)
                return Failure(RequestErrorKind.Validation, "The rename intent names another edition.");
            if (!CriteriaParticipantAdmission.AllowsLabel(renameIntent))
                return Pending();
        }

        var authorization = CriteriaLabelReleaseRules.Authorize(reservation, intent, terminal, renamed, replacement,
            replacementReservation, renameIntent);
        if (authorization is not { } authorized)
            return Failure(RequestErrorKind.Validation, "The source facts do not authorize this label release.");

        return await executor.ExecuteAsync(claim, owner => AggregateOutcome.CommitOnSuccess(
            owner.Release(request.EditionId, request.ClaimOperationId, request.Generation, request.EditionEventId,
                authorized.Reason, authorized.ReplacementConfirmationEventId)), context, ct).ConfigureAwait(false);
    }

    static Result Pending() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The exact committed label or edition source fact is not yet observable.", true));

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}

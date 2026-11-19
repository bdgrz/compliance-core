using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Confirms cleanup from matching committed witness-fence and edition-abort facts.</summary>
sealed class CompleteCriteriaPublicationAbortHandler(IAggregateExecutor executor,
    CriteriaLabelSourceVerifier source, IDomainEventReader events, IAggregateReader aggregates,
    ICriteriaFreshEditionHead? fresh = null) : IRequestHandler<CompleteCriteriaPublicationAbort>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CompleteCriteriaPublicationAbort> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A system reaction is required.");
        if (request.EditionId == Uuid.Empty || request.AttemptId == Uuid.Empty || request.SourceEventId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(request.Identifier))
            return Failure(RequestErrorKind.Validation, "The exact witness fence is required.");
        var fence = await source.ReadAsync<CriterionWitnessPinFenced>(CriteriaEntry.StreamFor(request.EditionId, request.Identifier),
            request.SourcePosition, request.SourceEventId, ct).ConfigureAwait(false);
        if (fence is null)
            return Failure(RequestErrorKind.Conflict, "The witness fence is not yet observable.");
        if (fence.EditionId != request.EditionId || fence.PublicationAttemptId != request.AttemptId ||
            fence.Identifier != request.Identifier || fence.PublicationAbortedEventId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "The fence names another attempt.");
        var aborted = await source.FindAsync<CriteriaPublicationAborted>(CriteriaEdition.AddressOf(request.EditionId),
            fence.PublicationAbortedEventId, ct).ConfigureAwait(false);
        if (aborted is null)
            return Failure(RequestErrorKind.Conflict, "The matching edition abort is not yet observable.");
        if (aborted.EditionId != fence.EditionId || aborted.DraftGeneration != fence.DraftGeneration ||
            aborted.PublicationAttemptId != fence.PublicationAttemptId || aborted.AttemptOrdinal != fence.AttemptOrdinal ||
            aborted.WitnessIdentifier != fence.Identifier || aborted.WitnessIncarnationId != fence.IncarnationId)
            return Failure(RequestErrorKind.Validation, "The edition abort and witness fence do not match.");
        if (fresh is null)
            return Pending();
        try
        {
            if (!await source.AllowsAbortAsync(aborted, ct).ConfigureAwait(false))
                return Pending();
            var edition = new CriteriaEdition(request.EditionId, request.AttemptId);
            var head = await CriteriaPublicationSourceHead.ReadAsync(events, edition, aborted.Metadata.EventId, ct).ConfigureAwait(false);
            var observed = await aggregates.HydrateAsync(edition, ct).ConfigureAwait(false);
            if (!head.Found || observed.CommittedStreamPosition != head.Next ||
                !await fresh.IsCurrentAsync(edition.Stream, edition.Id, head.Next, ct).ConfigureAwait(false))
                return Pending();
            return await executor.ExecuteAsync(new CriteriaEdition(request.EditionId, request.AttemptId), owner =>
                owner.CommittedStreamPosition != head.Next ? AggregateOutcome.Discard(Pending()) :
                    AggregateOutcome.CommitOnSuccess(owner.ConfirmPublicationAbortCleanup(request.AttemptId, request.SourceEventId)),
                context, ct).ConfigureAwait(false);
        }
        catch (CriteriaSourceUnavailableException) { return Pending(); }
        catch (EventStreamConcurrencyException) { return Pending(); }
        catch (FitzException) { return Pending(); }
        catch (IOException) { return Pending(); }
        catch (TimeoutException) { return Pending(); }
    }

    static Result Pending() => Result.Failure(new RequestError(RequestErrorKind.Conflict, "The edition source head is unavailable.", true));
    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}

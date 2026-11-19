using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Applies abort-first cleanup to the exact witness, even before any pin arrives.</summary>
sealed class FenceCriteriaPublicationWitnessHandler(IAggregateExecutor executor,
    CriteriaLabelSourceVerifier source, IDomainEventReader events, IAggregateReader aggregates,
    ICriteriaFreshEntryHead? fresh = null) : IRequestHandler<FenceCriteriaPublicationWitness>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<FenceCriteriaPublicationWitness> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A system reaction is required.");
        if (request.EditionId == Uuid.Empty || request.AttemptId == Uuid.Empty || request.SourceEventId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(request.Identifier))
            return Failure(RequestErrorKind.Validation, "The exact witness and abort identity are required.");
        var aborted = await source.ReadAsync<CriteriaPublicationAborted>(CriteriaEdition.AddressOf(request.EditionId),
            request.SourcePosition, request.SourceEventId, ct).ConfigureAwait(false);
        if (aborted is null)
            return Failure(RequestErrorKind.Conflict, "The publication abort is not yet observable.");
        if (aborted.EditionId != request.EditionId || aborted.PublicationAttemptId != request.AttemptId ||
            aborted.WitnessIdentifier != request.Identifier)
            return Failure(RequestErrorKind.Validation, "The source abort names another witness.");
        if (fresh is null)
            return Pending();
        try
        {
            if (!await source.AllowsAbortAsync(aborted, ct).ConfigureAwait(false))
                return Pending();
            var target = new CriteriaEntry(request.EditionId, request.Identifier, selectedPublicationAttemptId: request.AttemptId);
            var head = await CriteriaPublicationSourceHead.ReadAsync(events, target, Uuid.Empty, ct).ConfigureAwait(false);
            var observed = await aggregates.HydrateAsync(target, ct).ConfigureAwait(false);
            if (observed.CommittedStreamPosition != head.Next ||
                !await fresh.IsCurrentAsync(target.Stream, target.Id, head.Next, ct).ConfigureAwait(false))
                return Pending();
            return await executor.ExecuteAsync(new CriteriaEntry(request.EditionId, request.Identifier,
                selectedPublicationAttemptId: request.AttemptId), owner =>
                owner.CommittedStreamPosition != head.Next ? AggregateOutcome.Discard(Pending()) :
                    AggregateOutcome.CommitOnSuccess(owner.FenceWitness(aborted)), context, ct).ConfigureAwait(false);
        }
        catch (CriteriaSourceUnavailableException) { return Pending(); }
        catch (EventStreamConcurrencyException) { return Pending(); }
        catch (FitzException) { return Pending(); }
        catch (IOException) { return Pending(); }
        catch (TimeoutException) { return Pending(); }
    }

    static Result Pending() => Result.Failure(new RequestError(RequestErrorKind.Conflict, "The witness source head is unavailable.", true));
    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}

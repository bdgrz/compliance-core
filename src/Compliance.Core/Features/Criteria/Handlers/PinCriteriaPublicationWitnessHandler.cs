using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Pins the exact witness from a committed publication start.</summary>
sealed class PinCriteriaPublicationWitnessHandler(IAggregateExecutor executor,
    CriteriaLabelSourceVerifier source, IDomainEventReader events, IAggregateReader aggregates,
    ICriteriaFreshEntryHead? fresh = null, ICriteriaExactSourceReader? exact = null,
    ICriteriaSourceCutCoverage? coverage = null, ICriteriaParticipantCausalOrder? causal = null) : IRequestHandler<PinCriteriaPublicationWitness>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<PinCriteriaPublicationWitness> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A system reaction is required.");
        if (request.EditionId == Uuid.Empty || request.AttemptId == Uuid.Empty || request.SourceEventId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(request.Identifier))
            return Failure(RequestErrorKind.Validation, "The exact witness and start identity are required.");
        var started = await source.ReadAsync<CriteriaPublicationStarted>(CriteriaEdition.AddressOf(request.EditionId),
            request.SourcePosition, request.SourceEventId, ct).ConfigureAwait(false);
        if (started is null)
            return Failure(RequestErrorKind.Conflict, "The publication start is not yet observable.");
        if (started.EditionId != request.EditionId || started.PublicationAttemptId != request.AttemptId ||
            started.WitnessIdentifier != request.Identifier)
            return Failure(RequestErrorKind.Validation, "The source start names another witness.");
        if (fresh is null || exact is null || coverage is null)
            return Pending();
        try
        {
            if (!await source.AllowsPublicationAsync(started, ct).ConfigureAwait(false))
                return Pending();
            var selected = new CriteriaEntry(request.EditionId, request.Identifier, selectedPublicationAttemptId: request.AttemptId);
            var head = await CriteriaPublicationSourceHead.ReadAsync(events, selected, Uuid.Empty, ct).ConfigureAwait(false);
            var observed = await aggregates.HydrateAsync(selected, ct).ConfigureAwait(false);
            if (observed.CommittedStreamPosition != head.Next ||
                !await fresh.IsCurrentAsync(selected.Stream, selected.Id, head.Next, ct).ConfigureAwait(false))
                return Pending();
            // A later abort fence permanently resolves this start, even if the
            // witness was subsequently changed and no longer passes pin eligibility.
            // Verify the exact abort before settling the original signal.
            if (observed.SelectedWitnessFence is { } fenced)
            {
                var abort = await source.FindAsync<CriteriaPublicationAborted>(CriteriaEdition.AddressOf(request.EditionId),
                    fenced.PublicationAbortedEventId, ct).ConfigureAwait(false);
                if (abort is null || !MatchesFence(started, fenced, abort) ||
                    !await source.AllowsAbortAsync(abort, ct).ConfigureAwait(false))
                    return Pending();
                return Result.Success;
            }
            var edition = new CriteriaEdition(request.EditionId);
            var startRecord = await exact.FindAsync(edition.Stream, edition.Id, request.SourceEventId, ct).ConfigureAwait(false);
            if (startRecord?.Event is not CriteriaPublicationStarted exactStart || exactStart != started ||
                !await new CriteriaPublicationWitnessVerifier(exact, coverage, causal).VerifyAsync(request.EditionId,
                    request.Identifier, started.DraftGeneration, started.WitnessIncarnationId, null, head.Next, ct)
                    .ConfigureAwait(false))
                return Pending();
            return await executor.ExecuteAsync(new CriteriaEntry(request.EditionId, request.Identifier,
                selectedPublicationAttemptId: request.AttemptId), owner =>
            {
                if (owner.CommittedStreamPosition != head.Next)
                    return AggregateOutcome.Discard(Pending());
                // A historical pin that has been fenced cannot be reused as a live pin.
                if (owner.SelectedWitnessFence is not null)
                    return AggregateOutcome.Discard(Result.Success);
                return AggregateOutcome.CommitOnSuccess(owner.PinWitness(started));
            }, context, ct).ConfigureAwait(false);
        }
        catch (CriteriaSourceUnavailableException) { return Pending(); }
        catch (EventStreamConcurrencyException) { return Pending(); }
        catch (FitzException) { return Pending(); }
        catch (IOException) { return Pending(); }
        catch (TimeoutException) { return Pending(); }
    }

    internal static bool MatchesFence(CriteriaPublicationStarted start, CriterionWitnessPinFenced fence,
        CriteriaPublicationAborted abort) =>
        fence.EditionId == start.EditionId && fence.DraftGeneration == start.DraftGeneration &&
        fence.Identifier == start.WitnessIdentifier && fence.PublicationAttemptId == start.PublicationAttemptId &&
        fence.AttemptOrdinal == start.AttemptOrdinal && fence.IncarnationId == start.WitnessIncarnationId &&
        fence.PublicationAbortedEventId == abort.Metadata.EventId && abort.EditionId == start.EditionId &&
        abort.DraftGeneration == start.DraftGeneration && abort.PublicationAttemptId == start.PublicationAttemptId &&
        abort.AttemptOrdinal == start.AttemptOrdinal && abort.WitnessIdentifier == start.WitnessIdentifier &&
        abort.WitnessIncarnationId == start.WitnessIncarnationId;

    static Result Pending() => Result.Failure(new RequestError(RequestErrorKind.Conflict, "The witness source head is unavailable.", true));
    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}

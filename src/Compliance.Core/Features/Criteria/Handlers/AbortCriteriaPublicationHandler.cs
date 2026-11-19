using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Aborts only a source-proven target-side refusal at qualified participant heads.</summary>
sealed class AbortCriteriaPublicationHandler(IDomainEventReader events, IAggregateReader aggregates,
    IAggregateExecutor executor, CriteriaLabelSourceVerifier source, ICriteriaFreshEntryHead? witnessHead = null,
    ICriteriaFreshEditionHead? editionHead = null) : IRequestHandler<AbortCriteriaPublication>
{
    internal const string Reason = "witness refused";

    public async ValueTask<Result> HandleAsync(IRequestContext<AbortCriteriaPublication> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A system reaction is required.", false);
        if (request.EditionId == Uuid.Empty || request.AttemptId == Uuid.Empty || request.SourceEventId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(request.Identifier))
            return Failure(RequestErrorKind.Validation, "The exact witness refusal is required.", false);
        try
        {
            var witness = new CriteriaEntry(request.EditionId, request.Identifier, selectedPublicationAttemptId: request.AttemptId);
            var refusal = await source.ReadAsync<CriterionWitnessRefused>(witness.Stream, request.SourcePosition,
                request.SourceEventId, ct).ConfigureAwait(false);
            if (refusal is null)
                return Pending();
            if (refusal.Metadata.AggregateId != witness.Id || refusal.EditionId != request.EditionId ||
                refusal.PublicationAttemptId != request.AttemptId || refusal.Identifier != request.Identifier ||
                refusal.PublicationStartedEventId == Uuid.Empty)
                return Failure(RequestErrorKind.Validation, "The refusal names another attempt.", false);
            var edition = new CriteriaEdition(request.EditionId, request.AttemptId);
            var started = await source.FindAsync<CriteriaPublicationStarted>(edition.Stream, refusal.PublicationStartedEventId, ct).ConfigureAwait(false);
            if (started is null)
                return Pending();
            if (!Matches(started, refusal) || started.Metadata.AggregateId != edition.Id)
                return Failure(RequestErrorKind.Validation, "The refusal and edition start do not match.", false);
            if (!await source.AllowsPublicationAsync(started, ct).ConfigureAwait(false))
                return Pending();
            ulong readPosition = 0;
            await foreach (var item in events.ReadAsync(edition.Stream, 0, ct).ConfigureAwait(false))
            {
                if (item.Stream != edition.Stream || item.ResourceOffset != readPosition ||
                    item.Event.Metadata.AggregateId != edition.Id || item.Event.Metadata.IsAudit)
                    return Pending();
                readPosition = checked(readPosition + 1);
                if (item.Event is CriteriaPublicationAborted existing && existing.EditionId == refusal.EditionId &&
                    existing.DraftGeneration == refusal.DraftGeneration && existing.PublicationAttemptId == refusal.PublicationAttemptId &&
                    existing.AttemptOrdinal == refusal.AttemptOrdinal && existing.WitnessIdentifier == refusal.Identifier &&
                    existing.WitnessIncarnationId == refusal.IncarnationId && !string.IsNullOrWhiteSpace(existing.Reason))
                    return Result.Success;
            }
            if (witnessHead is null || editionHead is null)
                return Pending();
            var witnessSource = await CriteriaPublicationSourceHead.ReadAsync(events, witness, refusal.Metadata.EventId, ct).ConfigureAwait(false);
            var loadedWitness = await aggregates.HydrateAsync(witness, ct).ConfigureAwait(false);
            if (!witnessSource.Found || loadedWitness.CommittedStreamPosition != witnessSource.Next ||
                loadedWitness.SelectedWitnessRefusal?.Metadata.EventId != refusal.Metadata.EventId ||
                !await witnessHead.IsCurrentAsync(witness.Stream, witness.Id, witnessSource.Next, ct).ConfigureAwait(false))
                return Pending();
            var editionSource = await CriteriaPublicationSourceHead.ReadAsync(events, edition, started.Metadata.EventId, ct).ConfigureAwait(false);
            var loadedEdition = await aggregates.HydrateAsync(edition, ct).ConfigureAwait(false);
            if (!editionSource.Found || loadedEdition.CommittedStreamPosition != editionSource.Next ||
                loadedEdition.PendingPublication?.Metadata.EventId != started.Metadata.EventId ||
                !await editionHead.IsCurrentAsync(edition.Stream, edition.Id, editionSource.Next, ct).ConfigureAwait(false))
                return Pending();
            var result = await executor.ExecuteAsync(new CriteriaEdition(request.EditionId, request.AttemptId), owner =>
                owner.CommittedStreamPosition != editionSource.Next ? AggregateOutcome.Discard(Pending()) :
                    AggregateOutcome.CommitOnSuccess(owner.AbortPublication(request.AttemptId, Reason)), context, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                return result.Error!.Kind == RequestErrorKind.Conflict ? Pending() : result;
            await foreach (var item in events.ReadAsync(edition.Stream, 0, ct).ConfigureAwait(false))
                if (item.Event is CriteriaPublicationAborted aborted && aborted.EditionId == request.EditionId &&
                    aborted.PublicationAttemptId == request.AttemptId && aborted.AttemptOrdinal == refusal.AttemptOrdinal &&
                    aborted.DraftGeneration == refusal.DraftGeneration && aborted.WitnessIdentifier == refusal.Identifier &&
                    aborted.WitnessIncarnationId == refusal.IncarnationId && aborted.Reason == Reason &&
                    item.Event.Metadata.AggregateId == edition.Id && item.Stream == edition.Stream && !item.Event.Metadata.IsAudit)
                    return Result.Success;
            return Pending();
        }
        catch (CriteriaSourceUnavailableException) { return Pending(); }
        catch (EventStreamConcurrencyException) { return Pending(); }
        catch (FitzException) { return Pending(); }
        catch (IOException) { return Pending(); }
        catch (TimeoutException) { return Pending(); }
    }

    static bool Matches(CriteriaPublicationStarted start, CriterionWitnessRefused refusal) =>
        start.EditionId == refusal.EditionId && start.DraftGeneration == refusal.DraftGeneration &&
        start.PublicationAttemptId == refusal.PublicationAttemptId && start.AttemptOrdinal == refusal.AttemptOrdinal &&
        start.WitnessIdentifier == refusal.Identifier && start.WitnessIncarnationId == refusal.IncarnationId &&
        start.Metadata.EventId == refusal.PublicationStartedEventId;
    static Result Pending() => Failure(RequestErrorKind.Conflict, "The publication refusal or qualified source head is unavailable.", true);
    static Result Failure(RequestErrorKind kind, string message, bool transient) => Result.Failure(new RequestError(kind, message, transient));
}

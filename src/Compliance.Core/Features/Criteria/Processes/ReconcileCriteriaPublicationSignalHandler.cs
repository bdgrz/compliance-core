using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Processes.Models;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Processes;

/// <summary>Dispatches one exact publication signal independently of the shared source checkpoint.</summary>
sealed class ReconcileCriteriaPublicationSignalHandler(IDomainEventReader events, IImmutableContentStore content,
    IRequestBus bus) : IRequestHandler<ReconcileCriteriaPublicationSignal, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ReconcileCriteriaPublicationSignal> context,
        CancellationToken ct)
    {
        try
        { return await ReconcileAsync(context, ct).ConfigureAwait(false); }
        catch (CriteriaSourceUnavailableException) { return Pending("The publication source is unavailable."); }
        catch (ImmutableContentUnavailableException) { return Pending("The signal intent is unavailable."); }
        catch (ImmutableContentIntegrityException) { return Failure(RequestErrorKind.Validation, "The signal intent failed integrity verification.", false); }
        catch (EventStreamConcurrencyException) { return Pending("A publication participant changed."); }
        catch (FitzException) { return Pending("The publication source or append outcome is unresolved."); }
        catch (IOException) { return Pending("The publication source or append outcome is unresolved."); }
        catch (TimeoutException) { return Pending("The publication source or append outcome is unresolved."); }
    }

    async ValueTask<Result<ReceiverOutcome>> ReconcileAsync(IRequestContext<ReconcileCriteriaPublicationSignal> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A system worker is required.", false);
        var signal = context.Request;
        var bytes = await content.ReadVerifiedAsync(signal.Source.Stream.Realm, signal.Intent, ct).ConfigureAwait(false);
        await using var reader = events.ReadAsync(signal.Source.Stream, signal.Position, ct).GetAsyncEnumerator(ct);
        if (!await reader.MoveNextAsync().ConfigureAwait(false))
            return Pending("The exact publication source is not yet observable.");
        var record = reader.Current;
        var fact = record.Event;
        if (record.Stream != signal.Source.Stream || record.ResourceOffset != signal.Position ||
            fact.Metadata.EventId != signal.Source.EventId || fact.Metadata.AggregateVersion != signal.Source.AggregateVersion ||
            fact.Metadata.IsAudit || !CriteriaPublicationSignal.TryAddress(fact, out var expected, out var aggregateId) ||
            expected != record.Stream || fact.Metadata.AggregateId != aggregateId ||
            !bytes.Span.SequenceEqual(CriteriaPublicationSignal.Payload(record.Stream, fact, record.ResourceOffset)))
            return Failure(RequestErrorKind.Validation, "The publication signal differs from its committed source.", false);
        IRequest command = fact switch
        {
            CriteriaPublicationStarted e => new PinCriteriaPublicationWitness(e.EditionId, e.PublicationAttemptId, e.WitnessIdentifier,
                signal.Position, e.Metadata.EventId),
            CriterionWitnessPinned e => new CompleteCriteriaPublication(e.EditionId, e.PublicationAttemptId, e.Identifier,
                signal.Position, e.Metadata.EventId),
            CriterionWitnessRefused e => new AbortCriteriaPublication(e.EditionId, e.PublicationAttemptId, e.Identifier,
                signal.Position, e.Metadata.EventId),
            CriteriaPublicationAborted e => new FenceCriteriaPublicationWitness(e.EditionId, e.PublicationAttemptId, e.WitnessIdentifier,
                signal.Position, e.Metadata.EventId),
            CriterionWitnessPinFenced e => new CompleteCriteriaPublicationAbort(e.EditionId, e.PublicationAttemptId, e.Identifier,
                signal.Position, e.Metadata.EventId),
            _ => throw new InvalidOperationException("The signal event is not a publication participant fact."),
        };
        var outcome = await bus.SendAsync(command, context, ct).ConfigureAwait(false);
        if (!outcome.IsSuccess && outcome.Error!.Kind == RequestErrorKind.Conflict && command is PinCriteriaPublicationWitness pin)
        {
            // Pin failure alone is not refusal; only the target's qualified conditional decision may refuse.
            outcome = await bus.SendAsync(new DecideCriteriaWitnessRefusal(pin.EditionId, pin.AttemptId, pin.Identifier,
                pin.SourcePosition, pin.SourceEventId), context, ct).ConfigureAwait(false);
        }
        if (!outcome.IsSuccess)
        {
            var error = outcome.Error!;
            return Failure(error.Kind, error.Message, error.IsTransient || error.Kind == RequestErrorKind.Conflict);
        }
        return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Performed("criteria-publication-signal"));
    }

    static Result<ReceiverOutcome> Pending(string message) => Failure(RequestErrorKind.Conflict, message, true);
    static Result<ReceiverOutcome> Failure(RequestErrorKind kind, string message, bool transient) =>
        Result<ReceiverOutcome>.Failure(new RequestError(kind, message, transient));
}

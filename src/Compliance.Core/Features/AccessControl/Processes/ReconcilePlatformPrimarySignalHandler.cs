using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Processes.Models;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Processes;

/// <summary>Retries one stored exact-position primary participant attempt.</summary>
sealed class ReconcilePlatformPrimarySignalHandler(IDomainEventReader events, IImmutableContentStore content,
    PlatformPrimaryParticipant participant, IAggregateExecutor executor) : IRequestHandler<ReconcilePlatformPrimarySignal, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ReconcilePlatformPrimarySignal> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A system participant is required.", false);
        try
        {
            var signal = context.Request;
            var frozen = await content.ReadVerifiedAsync(signal.Source.Stream.Realm, signal.Intent, ct).ConfigureAwait(false);
            await using var source = events.ReadAsync(signal.Source.Stream, signal.SourcePosition, ct).GetAsyncEnumerator(ct);
            if (!await source.MoveNextAsync().ConfigureAwait(false))
                return Pending();
            var record = source.Current;
            if (record.Stream != PrimaryPlatformOperators.Address || record.Stream != signal.Source.Stream ||
                record.ResourceOffset != signal.SourcePosition || record.Event.Metadata.EventId != signal.Source.EventId ||
                record.Event.Metadata.AggregateId != PrimaryPlatformOperators.OwnerId || record.Event.Metadata.IsAudit ||
                record.Event.Metadata.AggregateVersion != signal.Source.AggregateVersion ||
                !PlatformPrimarySignalIntake.TryTarget(record.Event, out var user, out var operation) ||
                !frozen.Span.SequenceEqual(PlatformPrimarySignalIntake.Payload(signal.Source, user, operation)))
                return Failure(RequestErrorKind.Validation, "The primary signal differs from its frozen source.", false);
            var applied = record.Event switch
            {
                PrimaryPlatformOperatorSeatReserved => await participant.ApplyReservationAsync(signal.Source.EventId,
                    signal.SourcePosition, signal.TargetPosition, signal.TargetRevision, context, ct).ConfigureAwait(false),
                PrimaryPlatformOperatorSeatReleased => await participant.ApplyReleaseAsync(signal.Source.EventId,
                    signal.SourcePosition, signal.TargetPosition, signal.TargetRevision, context, ct).ConfigureAwait(false),
                _ => Result.Failure(new RequestError(RequestErrorKind.Validation, "Unsupported primary signal.")),
            };
            if (!applied.IsSuccess)
                return Pending();
            var actual = await participant.FindTargetOutcomeAsync(user, operation, signal.TargetPosition, ct).ConfigureAwait(false);
            if (actual is null)
                return Pending();
            var settled = await participant.RecordOutcomeAsync(user, actual.Value.Position, actual.Value.Fact.Metadata.EventId,
                signal.SourcePosition, signal.Source.EventId, context, ct).ConfigureAwait(false);
            if (!settled.IsSuccess)
                return Pending();
            if (record.Event is PrimaryPlatformOperatorSeatReserved { InitialExistingGrant: true } initial &&
                actual.Value.Fact is PlatformOperatorPrimaryMarkAccepted)
            {
                var completion = await executor.ExecuteAsync(new PrimaryPlatformOperators(), owner =>
                    AggregateOutcome.CommitOnSuccess(owner.CompleteBootstrap(initial.BootstrapPlanId)), context, ct).ConfigureAwait(false);
                if (!completion.IsSuccess)
                    return Pending();
            }
            return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Performed("platform-primary-participant"));
        }
        catch (EventStreamConcurrencyException) { return Pending(); }
        catch (FitzException) { return Pending(); }
        catch (IOException) { return Pending(); }
        catch (TimeoutException) { return Pending(); }
    }

    static Result<ReceiverOutcome> Pending() => Failure(RequestErrorKind.Conflict, "The primary participant is pending.", true);
    static Result<ReceiverOutcome> Failure(RequestErrorKind kind, string message, bool transient) =>
        Result<ReceiverOutcome>.Failure(new RequestError(kind, message, transient));
}

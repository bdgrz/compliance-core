using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>
/// Runs a root native admission on its first owner with the handler order of the design contract: resolve the
/// retry from the owner's accepted facts, validate guards, freeze the first response, then append the native
/// batch at the hydrated physical tail. A lost append race rehydrates and resolves again by logical id, so an
/// identical concurrent request that already committed is replayed and never appended twice.
/// </summary>
public sealed class NativeOperationExecutor(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content)
{
    /// <summary>Attempts before an unresolved append race is surfaced to the caller.</summary>
    internal const int MaximumAttempts = 4;

    /// <summary>Executes the operation and returns its closed acceptance outcome.</summary>
    public async ValueTask<Result<AcceptedAppendResult>> ExecuteAsync<TAggregate>(
        NativeOperation<TAggregate> operation, IExecutionContext context, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var hydrated = await reader.HydrateAsync(operation.Hydrate(), ct).ConfigureAwait(false);
            var acceptance = operation.Acceptance(hydrated);
            var decided = await DecideAsync(operation, hydrated, acceptance, ct).ConfigureAwait(false);
            if (decided is not null)
                return Result<AcceptedAppendResult>.Success(decided);
            var prepared = await operation.Prepare(hydrated, ct).ConfigureAwait(false);
            if (!prepared.IsSuccess)
                return await ResolveFailureAsync(operation, prepared.Error!, ct).ConfigureAwait(false);
            var frozen = await FirstResponses.FreezeAsync(content, operation.Binding.Realm, prepared.Value!, ct)
                .ConfigureAwait(false);
            TAggregate? committed = null;
            try
            {
                var step = await executor.ExecuteAsync<TAggregate, Step>(operation.Hydrate(), aggregate =>
                {
                    committed = aggregate;
                    var current = operation.Acceptance(aggregate);
                    switch (current.Decide(operation.Binding))
                    {
                        case NativeAcceptanceDecision.Replayed:
                            return AggregateOutcome.Discard(Result<Step>.Success(new Step(null, current.SelectedFact)));
                        case NativeAcceptanceDecision.Conflict:
                            return AggregateOutcome.Discard(Result<Step>.Success(
                                new Step(AcceptedAppendResult.Conflict.Instance, null)));
                    }

                    var applied = operation.Apply(aggregate, frozen);
                    if (!applied.IsSuccess)
                        return AggregateOutcome.Discard(Result<Step>.Failure(applied.Error!));
                    var fact = current.SelectedFact ?? throw new InvalidOperationException(
                        "Apply must raise the native fact that carries the selected logical id.");
                    if (current.SelectedOperation!.FirstResponse != frozen)
                        throw new InvalidOperationException("The native fact must carry the frozen first response.");
                    return AggregateOutcome.Commit(Result<Step>.Success(new Step(
                        new AcceptedAppendResult.Committed(NativeFactReference.Of(aggregate.Stream, fact), 0, frozen,
                            prepared.Value!), null)));
                }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return await ResolveFailureAsync(operation, step.Error!, ct).ConfigureAwait(false);
                if (step.Value!.Replay is { } replay)
                    return Result<AcceptedAppendResult>.Success(
                        await ReplayAsync(operation, committed!, replay, ct).ConfigureAwait(false));
                if (step.Value.Done is AcceptedAppendResult.Conflict conflict)
                    return Result<AcceptedAppendResult>.Success(conflict);
                var done = (AcceptedAppendResult.Committed)step.Value.Done!;
                return Result<AcceptedAppendResult>.Success(
                    done with { NextPhysicalPositionExclusive = committed!.CommittedStreamPosition });
            }
            catch (EventStreamConcurrencyException)
            {
                // The append lost the owner's OCC. An identical concurrent request may have committed: the next
                // attempt rehydrates and resolves the same logical id before deciding anything.
                if (attempt >= MaximumAttempts)
                {
                    var resolved = await RecheckAsync(operation, ct).ConfigureAwait(false);
                    if (resolved is not null)
                        return Result<AcceptedAppendResult>.Success(resolved);
                    throw;
                }
            }
        }
    }

    async Task<Result<AcceptedAppendResult>> ResolveFailureAsync<TAggregate>(NativeOperation<TAggregate> operation,
        RequestError error, CancellationToken ct) where TAggregate : Aggregate
    {
        var resolved = await RecheckAsync(operation, ct).ConfigureAwait(false);
        return resolved is null ? Result<AcceptedAppendResult>.Failure(error)
            : Result<AcceptedAppendResult>.Success(resolved);
    }

    async Task<AcceptedAppendResult?> RecheckAsync<TAggregate>(NativeOperation<TAggregate> operation,
        CancellationToken ct) where TAggregate : Aggregate
    {
        var current = await reader.HydrateAsync(operation.Hydrate(), ct).ConfigureAwait(false);
        return await DecideAsync(operation, current, operation.Acceptance(current), ct).ConfigureAwait(false);
    }

    async Task<AcceptedAppendResult?> DecideAsync<TAggregate>(NativeOperation<TAggregate> operation,
        TAggregate hydrated, NativeAcceptance acceptance, CancellationToken ct) where TAggregate : Aggregate
    {
        switch (acceptance.Decide(operation.Binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return await ReplayAsync(operation, hydrated, acceptance.SelectedFact!, ct).ConfigureAwait(false);
            case NativeAcceptanceDecision.Conflict:
                return AcceptedAppendResult.Conflict.Instance;
            default:
                return null;
        }
    }

    async Task<AcceptedAppendResult> ReplayAsync<TAggregate>(NativeOperation<TAggregate> operation,
        TAggregate owner, DomainEvent fact, CancellationToken ct) where TAggregate : Aggregate
    {
        var reference = ((INativeOperationFact)fact).FirstResponse
            ?? throw new ImmutableContentIntegrityException("The accepted root fact carries no first response.");
        var response = await FirstResponses.ReplayAsync(content, operation.Binding.Realm, reference, ct)
            .ConfigureAwait(false);
        return new AcceptedAppendResult.Replayed(NativeFactReference.Of(owner.Stream, fact), reference, response);
    }

    sealed record Step(AcceptedAppendResult? Done, DomainEvent? Replay);
}

using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceUse.Handlers;

/// <summary>
///     The generic receiver that settles one use of a source owner by its exact key. A concrete pair declares how to
///     open its owner, how to verify its terminal source event and the disposition that event requires, and how to
///     raise its settlement fact. The settlement carries the exact grant when one exists; with no grant it fences the
///     key so a delayed acquisition is refused. Elapsed time, lease expiry and missing reads never settle a use.
/// </summary>
/// <typeparam name="TOwner">The source owner that embeds <see cref="SourceUseReservations" />.</typeparam>
/// <typeparam name="TRequest">The pair's settle command.</typeparam>
public abstract class SettleSourceUseHandler<TOwner, TRequest>(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<TRequest, ReceiverOutcome>
    where TOwner : Aggregate, ISourceUseOwner
    where TRequest : ISourceUseSettlement
{
    const int MaximumAttempts = 4;

    /// <summary>The immutable content store the receiver reads and writes.</summary>
    protected IImmutableContentStore Content { get; } = content;

    /// <inheritdoc />
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<TRequest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.OperationId == Uuid.Empty || request.Generation < 0 ||
            request.Target != Open(request, request.CommandId).Stream)
            return SourceUseReceiverSupport.Permanent("The settlement names another target stream, operation or generation.");
        var selection = ReceiptSelection(request);
        SourceUseSource source;
        if (selection is not null)
        {
            var preparation = await NativeReceiverPreparation.PrepareAsync<TOwner, SourceUseSource>(reader,
                () => Open(request, request.CommandId).ForSourceUses(selection),
                (owner, token) => DecideReceiptAsync(request, owner, selection, token),
                (_, token) => PrepareAsync(request, true, token), ct).ConfigureAwait(false);
            if (!preparation.IsSuccess)
                return Result<ReceiverOutcome>.Failure(preparation.Error!);
            if (preparation.Value!.Outcome is { } known)
                return known;
            source = preparation.Value.Value!;
        }
        else
        {
            var preparation = await PrepareAsync(request, false, ct).ConfigureAwait(false);
            if (!preparation.IsSuccess)
                return Result<ReceiverOutcome>.Failure(preparation.Error!);
            source = preparation.Value!;
        }
        var required = source.Disposition!.Value;
        var provenance = SourceUseReceiverSupport.Child(request.CommandId, source.CommandKind,
            request.Source.EventId, source.Header, request.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var settle = SettleOperationWithDisposition();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var preparation = await NativeReceiverPreparation.PrepareAsync<TOwner, SettlementPrepared>(reader,
                    () => Open(request, request.CommandId).ForSourceUses(source.Key),
                    owner => Decide(owner, binding, source.Key), async (owner, token) =>
                    {
                        try
                        {
                            var currentGrant = owner.Uses.GrantOf(source.Key);
                            var frozenIntent = await Content.WriteJsonAsync(request.Target.Realm,
                                Uuid.CreateVersion5(request.CommandId, $"source-use-settlement-intent:{currentGrant?.ReservationGeneration ?? 0}"),
                                SourceUseAuthorizationIntent.Schema, new SourceUseAuthorizationIntent(source.Header,
                                    source.ParentIntent, source.Key, source.Requirement, currentGrant, required), token).ConfigureAwait(false);
                            return Result<SettlementPrepared>.Success(new SettlementPrepared(frozenIntent, currentGrant));
                        }
                        catch (ImmutableContentIntegrityException error) when (selection is not null)
                        {
                            token.ThrowIfCancellationRequested();
                            return Result<SettlementPrepared>.Failure(new RequestError(RequestErrorKind.Conflict,
                                error.Message, error is ImmutableContentUnavailableException));
                        }
                    }, ct).ConfigureAwait(false);
                if (!preparation.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(preparation.Error!);
                if (preparation.Value!.Outcome is { } known)
                    return known;
                var (intent, held) = (preparation.Value.Value!.Intent, preparation.Value.Value.Grant);
                var step = await executor.ExecuteAsync(Open(request, request.CommandId).ForSourceUses(source.Key), fresh =>
                {
                    if (Decide(fresh, binding, source.Key) is { } decided)
                        return AggregateOutcome.Discard(Result<Step>.Success(new Decided(decided)));
                    if (fresh.Uses.GrantOf(source.Key)?.ReservationGeneration != held?.ReservationGeneration)
                        return AggregateOutcome.Discard(Result<Step>.Success(new Stale()));
                    var raised = settle(fresh, provenance, intent, request.Generation, required);
                    return raised.IsSuccess
                        ? AggregateOutcome.Commit(Result<Step>.Success(new Decided(SourceUseReceiverSupport.Applied(
                            NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!)))))
                        : AggregateOutcome.Discard(Result<Step>.Failure(raised.Error!));
                }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                if (step.Value is Decided done)
                    return done.Outcome;
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The owner changed concurrently; rehydrate and decide again.
            }

            if (attempt >= MaximumAttempts)
                return SourceUseReceiverSupport.Transient("The source owner kept changing while the use was settled.");
        }
    }

    /// <summary>An optional exact historical key selected only as a locator before native receipt verification.</summary>
    protected virtual ReservationKey? ReceiptSelection(TRequest request) => null;

    /// <summary>Optionally binds an actual selected settlement and its stored terms to its native terminal source.</summary>
    protected virtual ValueTask<Result<SourceUseSource>?> VerifyReceiptSourceAsync(TRequest request, TOwner owner,
        CancellationToken ct) => ValueTask.FromResult<Result<SourceUseSource>?>(null);

    async ValueTask<Result<ReceiverOutcome>?> DecideReceiptAsync(TRequest request, TOwner owner, ReservationKey selection,
        CancellationToken ct)
    {
        var receipt = await VerifyReceiptSourceAsync(request, owner, ct).ConfigureAwait(false);
        if (receipt is null)
            return null;
        var result = receipt.Value;
        if (!result.IsSuccess)
            return Result<ReceiverOutcome>.Failure(result.Error!);
        var source = result.Value!;
        if (source.Key != selection || source.Key.Target != request.Target || source.Key.RootOperationId != request.OperationId ||
            source.Disposition is not { } required)
            return SourceUseReceiverSupport.Permanent("The settlement differs from the frozen source use requirement.");
        if (request.Disposition != required)
            return SourceUseReceiverSupport.Permanent("The disposition does not match the verified terminal source event kind.");
        var provenance = SourceUseReceiverSupport.Child(request.CommandId, source.CommandKind,
            request.Source.EventId, source.Header, request.Target, clock.GetUtcNow());
        return Decide(owner, AcceptedOperationBinding.Of(provenance), source.Key);
    }

    async ValueTask<Result<SourceUseSource>> PrepareAsync(TRequest request, bool classify, CancellationToken ct)
    {
        try
        {
            var verified = await VerifyAsync(request, ct).ConfigureAwait(false);
            if (!verified.IsSuccess)
                return Result<SourceUseSource>.Failure(verified.Error!);
            var source = verified.Value!;
            if (source.Key.Target != request.Target || source.Key.RootOperationId != request.OperationId ||
                source.Disposition is not { } required)
                return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The settlement differs from the frozen source use requirement.", false));
            return request.Disposition != required
                ? Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The disposition does not match the verified terminal source event kind.", false))
                : verified;
        }
        catch (ImmutableContentIntegrityException error) when (classify)
        {
            ct.ThrowIfCancellationRequested();
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                error.Message, error is ImmutableContentUnavailableException));
        }
    }

    /// <summary>Opens the target owner of <paramref name="request" />, selecting <paramref name="selectedCommandId" />.</summary>
    protected abstract TOwner Open(TRequest request, Uuid selectedCommandId);

    /// <summary>
    ///     Verifies the terminal source event from the authoritative source owner, derives the frozen reservation key
    ///     and requirement, and states the disposition that event kind requires. A terminal event that is not visible
    ///     yet is a transient failure.
    /// </summary>
    protected abstract ValueTask<Result<SourceUseSource>> VerifyAsync(TRequest request, CancellationToken ct);

    /// <summary>
    ///     The pair's settle operation: raises its settlement fact on the owner. A method that returns the operation,
    ///     with explicitly typed lambda parameters, keeps the owner mutation visible to the aggregate route inventory.
    ///     A pair whose settlement fact depends on how the use ends overrides
    ///     <see cref="SettleOperationWithDisposition" /> instead.
    /// </summary>
    protected virtual Func<TOwner, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation() =>
        throw new NotSupportedException("The pair declares no disposition-independent settle operation.");

    /// <summary>
    ///     The pair's settle operation when the settlement fact records the disposition the verified terminal source
    ///     event required. The default wraps <see cref="SettleOperation" /> and ignores the disposition.
    /// </summary>
    protected virtual Func<TOwner, OperationProvenance, ImmutableContentRef, long, SourceUseSettlementDisposition, Result>
        SettleOperationWithDisposition()
    {
        return (TOwner owner, OperationProvenance provenance, ImmutableContentRef intent, long generation,
            SourceUseSettlementDisposition disposition) =>
            SettleOperation(disposition)(owner, provenance, intent, generation);
    }

    /// <summary>
    ///     The settle operation for a verified disposition. A pair whose owner must distinguish a closure fence from a
    ///     canceled-parent clearing in its own state overrides this to hand the disposition to its settlement fact; the
    ///     default ignores it and uses <see cref="SettleOperation()" />.
    /// </summary>
    protected virtual Func<TOwner, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation(
        SourceUseSettlementDisposition disposition) => SettleOperation();

    static Result<ReceiverOutcome>? Decide(TOwner owner, AcceptedOperationBinding binding, ReservationKey key)
    {
        switch (owner.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream,
                    owner.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        return owner.Uses.Find(key) is { IsSettled: true } entry
            ? SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream, entry.SettleFact!))
            : null;
    }

    sealed record SettlementPrepared(ImmutableContentRef Intent, ReservationGrant? Grant);

    abstract record Step;

    sealed record Decided(Result<ReceiverOutcome> Outcome) : Step;

    sealed record Stale : Step;
}

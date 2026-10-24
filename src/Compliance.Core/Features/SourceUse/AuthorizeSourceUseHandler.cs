using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceUse;

/// <summary>
///     The generic receiver that acquires one use of a source owner. A concrete pair declares how to open its owner,
///     how to verify its proven source acceptance, what the pinned source content must satisfy, how to raise its grant
///     fact and how to reject its source when the pinned content is no longer eligible; everything else is shared.
///     An exact retry returns the original grant, a settled key is a fence that refuses late acquisition, and an
///     unknown append is retried against the rehydrated owner.
/// </summary>
/// <typeparam name="TOwner">The source owner that embeds <see cref="SourceUseReservations" />.</typeparam>
/// <typeparam name="TRequest">The pair's authorize command.</typeparam>
public abstract class AuthorizeSourceUseHandler<TOwner, TRequest>(IAggregateReader reader,
    IAggregateExecutor executor, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<TRequest, ReceiverOutcome>
    where TOwner : Aggregate, ISourceUseOwner
    where TRequest : ISourceUseAuthorization
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
            return SourceUseReceiverSupport.Permanent("The authorization names another target stream, operation or generation.");
        var verified = await VerifyAsync(request, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var source = verified.Value!;
        if (source.SupersededReason is { } reason)
            return source.RequiresAbort
                ? Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected(reason, null))
                : SourceUseReceiverSupport.Superseded(reason, null);
        if (source.Key.Target != request.Target || source.Key.RootOperationId != request.OperationId ||
            source.Requirement.SourceOwner != request.Target)
            return SourceUseReceiverSupport.Permanent("The authorization differs from the frozen source use requirement.");

        var provenance = SourceUseReceiverSupport.Child(request.CommandId, source.CommandKind,
            request.Source.EventId, source.Header, request.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var grant = GrantOperation();
        var intent = await Content.WriteJsonAsync(request.Target.Realm,
            Uuid.CreateVersion5(request.CommandId, "source-use-authorization-intent"),
            SourceUseAuthorizationIntent.Schema, new SourceUseAuthorizationIntent(source.Header, source.ParentIntent,
                source.Key, source.Requirement, null, null), ct).ConfigureAwait(false);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var owner = await reader.HydrateAsync(Open(request, request.CommandId), ct).ConfigureAwait(false);
                if (Decide(owner, binding, source.Key) is { } early)
                    return early;
                if (FencedReason(owner, source.Key) is { } fenced)
                    return SourceUseReceiverSupport.Superseded(fenced, null);
                var pinned = await VerifyPinnedAsync(owner, source.Requirement, ct).ConfigureAwait(false);
                if (!pinned.IsSuccess)
                    return pinned.Error!.IsTransient
                        ? Result<ReceiverOutcome>.Failure(pinned.Error)
                        : await RefuseAsync(request, source, pinned.Error.Message, context, ct).ConfigureAwait(false);

                var step = await executor.ExecuteAsync(Open(request, request.CommandId), fresh =>
                {
                    if (Decide(fresh, binding, source.Key) is { } decided)
                        return AggregateOutcome.Discard(Result<Step>.Success(new Decided(decided)));
                    if (FencedReason(fresh, source.Key) is { } freshFenced)
                        return AggregateOutcome.Discard(Result<Step>.Success(new Decided(
                            SourceUseReceiverSupport.Superseded(freshFenced, null))));
                    if (!IsPinned(fresh, source.Requirement))
                        return AggregateOutcome.Discard(Result<Step>.Success(new Stale()));
                    var raised = grant(fresh, provenance, intent, request.Generation);
                    return raised.IsSuccess
                        ? AggregateOutcome.Commit(Result<Step>.Success(new Decided(SourceUseReceiverSupport.Applied(
                            NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!)))))
                        : AggregateOutcome.Discard(Result<Step>.Failure(raised.Error!));
                }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                return step.Value is Decided done
                    ? done.Outcome
                    : await RefuseAsync(request, source, "The pinned source content changed before the use was granted.",
                        context, ct).ConfigureAwait(false);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The owner changed concurrently; rehydrate and decide again.
            }
        }
    }

    /// <summary>Opens the target owner of <paramref name="request" />, selecting <paramref name="selectedCommandId" />.</summary>
    protected abstract TOwner Open(TRequest request, Uuid selectedCommandId);

    /// <summary>
    ///     Verifies the proven source acceptance from the authoritative source owner and derives the frozen reservation
    ///     key, requirement and header. A transient failure waits for the source; a permanent failure quarantines.
    /// </summary>
    protected abstract ValueTask<Result<SourceUseSource>> VerifyAsync(TRequest request, CancellationToken ct);

    /// <summary>
    ///     Checks the pinned content of the hydrated owner against the requirement, reading immutable content when the
    ///     rule needs it. A permanent failure rejects the source; a transient one waits.
    /// </summary>
    protected abstract ValueTask<Result> VerifyPinnedAsync(TOwner owner, SourceUseRequirement requirement,
        CancellationToken ct);

    /// <summary>
    ///     A reason when the owner itself already refuses this key before any grant (a cancellation fence the owner
    ///     installed against the source operation), which ends the receiver as a terminal no-op. The default has none.
    /// </summary>
    protected virtual string? FencedReason(TOwner owner, ReservationKey key) => null;

    /// <summary>True when the owner's current state is still exactly the pinned content (revision and event).</summary>
    protected abstract bool IsPinned(TOwner owner, SourceUseRequirement requirement);

    /// <summary>
    ///     The pair's grant operation: raises its grant fact on the owner. A method that returns the operation, with
    ///     explicitly typed lambda parameters, keeps the owner mutation visible to the aggregate route inventory.
    /// </summary>
    protected abstract Func<TOwner, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation();

    /// <summary>
    ///     Rejects the source operation and fences the key because the pinned content is not eligible. Returns the
    ///     closed result, normally a supersession that ends the process.
    /// </summary>
    protected abstract ValueTask<Result<ReceiverOutcome>> RefuseAsync(TRequest request, SourceUseSource source,
        string reason, IExecutionContext context, CancellationToken ct);

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

        if (owner.Uses.Find(key) is not { } entry)
            return null;
        return entry.IsActive
            ? SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream, entry.GrantFact!))
            : SourceUseReceiverSupport.Superseded("The use was settled before it was granted; the key is fenced.",
                NativeFactReference.Of(owner.Stream, entry.SettleFact!));
    }

    abstract record Step;

    sealed record Decided(Result<ReceiverOutcome> Outcome) : Step;

    sealed record Stale : Step;
}

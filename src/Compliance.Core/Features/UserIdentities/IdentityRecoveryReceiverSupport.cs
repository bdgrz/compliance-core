using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     What every receiver of the identity recovery process shares: verifying a source proof and the frozen intent
///     against the command, deriving the receiver's operation provenance from them, and applying one accepted fact
///     on a target owner with replay, supersession, rejection and optimistic-concurrency handling.
/// </summary>
public sealed class IdentityRecoveryReceiverSupport(ISourceFactResolver resolver, IImmutableContentStore content,
    IAggregateExecutor executor, IAggregateReader reader)
{
    const int MaximumAttempts = 5;

    /// <summary>The content store the receivers read their frozen intent from.</summary>
    public IImmutableContentStore Content => content;

    /// <summary>The reader the receivers use for the exact owners they check.</summary>
    public IAggregateReader Reader => reader;

    /// <summary>The resolver that re-reads the facts behind a proof.</summary>
    public ISourceFactResolver Resolver => resolver;

    /// <summary>The permanent error for a proof, intent or command that does not match the admission.</summary>
    public static Result<T> Invalid<T>(string message) where T : notnull =>
        Result<T>.Failure(new RequestError(RequestErrorKind.Conflict, message, false));

    /// <summary>The permanent failure for a proof, intent or command that does not match the admission.</summary>
    public static Result InvalidResult(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message, false));

    /// <summary>The transient error for a dependency that is not there yet.</summary>
    public static Result<ReceiverOutcome> Wait(string message) =>
        Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, message, true));

    /// <summary>A terminal no-op because a proven later fact replaced this step.</summary>
    public static Result<ReceiverOutcome> Superseded(string reason) =>
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Superseded(reason, null));

    /// <summary>A definite conflict: the driver rejects the preparation instead of retrying.</summary>
    public static Result<ReceiverOutcome> Rejected(string reason) =>
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected(reason));

    /// <summary>The platform realm every owner of the identity recovery process lives in.</summary>
    public const string Realm = "bdgrz";

    /// <summary>Re-reads the record behind a proof; any mismatch is a permanent conflict.</summary>
    public async Task<Result<VerifiedSourceProof<TFact>>> VerifySourceAsync<TFact>(SourceFactProof<TFact> proof,
        CancellationToken ct) where TFact : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(proof);
        try
        {
            return Result<VerifiedSourceProof<TFact>>.Success(await resolver.VerifyAsync(proof, ct).ConfigureAwait(false));
        }
        catch (SourceFactUnavailableException exception)
        {
            return Result<VerifiedSourceProof<TFact>>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact source proof is not visible yet: {exception.Message}", true));
        }
        catch (SourceFactVerificationException exception)
        {
            return Result<VerifiedSourceProof<TFact>>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The source proof does not verify: {exception.Message}", false));
        }
    }

    /// <summary>Verifies a proof and checks the fact it carries is of this recovery: operation, challenge and intent.</summary>
    public async Task<Result<VerifiedSourceProof<TFact>>> VerifyRecoveryFactAsync<TFact>(SourceFactProof<TFact> proof,
        IIdentityRecoveryStepReceiver request, CancellationToken ct) where TFact : DomainEvent, IIdentityRecoveryFact
    {
        var verified = await VerifySourceAsync(proof, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return verified;
        var fact = verified.Value!.Facts;
        return verified.Value.Source.Realm != Realm || fact.ChallengeId != request.ChallengeId ||
               fact.Operation.RootOperationId != request.OperationId || fact.Generation != request.Generation ||
               fact.Intent != request.Intent
            ? Invalid<VerifiedSourceProof<TFact>>("The recovery fact does not carry this operation, generation and intent.")
            : verified;
    }

    /// <summary>Reads and checks the frozen intent a receiver command names.</summary>
    public async Task<Result<IdentityRecoveryIntent>> ReadIntentAsync(IIdentityRecoveryStepReceiver request,
        CancellationToken ct)
    {
        IdentityRecoveryIntent intent;
        try
        {
            intent = await content.ReadJsonAsync<IdentityRecoveryIntent>(Realm, request.Intent, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return Result<IdentityRecoveryIntent>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact immutable dependency is not visible yet: {exception.Message}", true));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Invalid<IdentityRecoveryIntent>($"The frozen intent does not verify: {exception.Message}");
        }

        return intent.Header.RootOperationId != request.OperationId || intent.ChallengeId != request.ChallengeId ||
               request.OperationId != request.ChallengeId
            ? Invalid<IdentityRecoveryIntent>("The intent names another operation or challenge.")
            : Result<IdentityRecoveryIntent>.Success(intent);
    }

    /// <summary>The provenance of a receiver's accepted fact on <paramref name="owner" />.</summary>
    public static OperationProvenance Provenance(IIdentityRecoveryStepReceiver request, string commandKind,
        EventStreamAddress owner, IdentityRecoveryIntent intent, Uuid causationEventId) =>
        new(request.CommandId, request.OperationId, causationEventId, commandKind, intent.Header.OriginalActor, owner,
            intent.ChallengeId.ToString(), request.ExpectedRevision is >= 0 and var revision ? (ulong)revision : null,
            intent.Header.AuthoredIntentSha256, intent.Header.AcceptedAt, owner, owner);

    /// <summary>The recovery owner of a challenge.</summary>
    public static IdentityRecovery RecoveryOf(Uuid challengeId, Uuid commandId = default) =>
        new(challengeId, commandId);

    /// <summary>The stream of an identity slot.</summary>
    public static EventStreamAddress IdentityStream(Uuid identityId) => new UserIdentity(identityId).Stream;

    /// <summary>The technical owner of a recovery's completion notice.</summary>
    public static EventStreamAddress NoticeStream(Uuid challengeId) =>
        new(Realm, "identity-recovery-notices", challengeId.ToString());

    /// <summary>
    ///     Reads the protection proofs a swap acceptance names and proves both are the exact protection of this
    ///     recovery's old and replacement slots. Any mismatch is a permanent conflict.
    /// </summary>
    public async Task<Result<IdentityRecoveryProtectionProofs>> VerifyProtectionAsync(
        ImmutableContentRef reference, IIdentityRecoveryStepReceiver request, IdentityRecoveryIntent intent,
        CancellationToken ct)
    {
        IdentityRecoveryProtectionProofs proofs;
        try
        {
            proofs = await content.ReadJsonAsync<IdentityRecoveryProtectionProofs>(Realm, reference, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return Result<IdentityRecoveryProtectionProofs>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact immutable dependency is not visible yet: {exception.Message}", true));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Invalid<IdentityRecoveryProtectionProofs>($"The protection proofs do not verify: {exception.Message}");
        }

        foreach (var (proof, identity, role) in new[]
                 {
                     (proofs.Old, intent.OldIdentityId, RecoveryIdentityRole.Old),
                     (proofs.Replacement, intent.ReplacementIdentityId, RecoveryIdentityRole.Replacement),
                 })
        {
            var verified = await VerifySourceAsync(proof, ct).ConfigureAwait(false);
            if (!verified.IsSuccess)
                return Result<IdentityRecoveryProtectionProofs>.Failure(verified.Error!);
            var fact = verified.Value!.Facts;
            if (verified.Value.Source != IdentityStream(identity) || fact.Owner != verified.Value.Source ||
                fact.Role != role || fact.ChallengeId != request.ChallengeId || fact.AccountId != intent.AccountId ||
                fact.Intent != request.Intent)
                return Invalid<IdentityRecoveryProtectionProofs>(
                    "A protection proof is not the exact protection of this recovery slot.");
        }

        return Result<IdentityRecoveryProtectionProofs>.Success(proofs);
    }

    /// <summary>The Unit result content a terminal fact references as its operation result.</summary>
    public async Task<ImmutableContentRef> UnitResultAsync(Uuid rootOperationId, CancellationToken ct)
    {
        var unit = await content.WriteJsonAsync(Realm, Uuid.CreateVersion5(rootOperationId, UnitResult.Schema),
            UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
        return await content.WriteJsonAsync(Realm, Uuid.CreateVersion5(rootOperationId, "mutation-result-content.v1"),
            "mutation-result-content.v1", new MutationResultContent(UnitResult.Schema, new StandardResponseBody(unit)), ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Accepts one fact on the target owner. Replays return the original fact, conflicting reuse of the logical
    ///     id fails permanently, <paramref name="early" /> may stop with a wait, a supersession, a rejection or an
    ///     existing proof, and a lost append race is retried against the rehydrated owner.
    /// </summary>
    public ValueTask<Result<ReceiverOutcome>> ApplyAsync<TAggregate>(Func<TAggregate> create,
        Func<TAggregate, NativeAcceptance> acceptance, AcceptedOperationBinding binding,
        Func<TAggregate, Result<ReceiverOutcome>?> early, Func<TAggregate, Result> operate, IExecutionContext context,
        CancellationToken ct, Func<TAggregate, Result>? preparedGuard = null) where TAggregate : Aggregate =>
        ApplyAsync<TAggregate, EmptyPreparation>(create, acceptance, binding,
            (owner, _) => ValueTask.FromResult(preparedGuard?.Invoke(owner) is { IsSuccess: false } failed
                ? Result<EmptyPreparation>.Failure(failed.Error!)
                : Result<EmptyPreparation>.Success(EmptyPreparation.Instance)),
            (owner, _) => early(owner), (owner, _) => operate(owner), context, ct);

    /// <summary>Prepares mutable dependencies only after selected receipt knowledge, and guards that exact target cut through append.</summary>
    public async ValueTask<Result<ReceiverOutcome>> ApplyAsync<TAggregate, TPrepared>(Func<TAggregate> create,
        Func<TAggregate, NativeAcceptance> acceptance, AcceptedOperationBinding binding,
        Func<TAggregate, CancellationToken, ValueTask<Result<TPrepared>>> prepare,
        Func<TAggregate, TPrepared, Result<ReceiverOutcome>?> early, Func<TAggregate, TPrepared, Result> operate,
        IExecutionContext context, CancellationToken ct) where TAggregate : Aggregate where TPrepared : class
    {
        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var preparation = await NativeReceiverPreparation.PrepareAsync<TAggregate, ReceiverCandidate<TPrepared>>(reader,
                create, owner => Decide(acceptance(owner), binding, owner.Stream), async (owner, cancel) =>
                {
                    var cut = IdentityOwnerPreparationCut.Of(owner);
                    var dependency = await prepare(owner, cancel).ConfigureAwait(false);
                    if (!dependency.IsSuccess)
                        return Result<ReceiverCandidate<TPrepared>>.Failure(dependency.Error!);
                    var stopped = early(owner, dependency.Value!);
                    return stopped is { IsSuccess: false } failed
                        ? Result<ReceiverCandidate<TPrepared>>.Failure(failed.Error!)
                        : Result<ReceiverCandidate<TPrepared>>.Success(new ReceiverCandidate<TPrepared>(cut, dependency.Value!, stopped));
                }, ct).ConfigureAwait(false);
            if (!preparation.IsSuccess)
                return Result<ReceiverOutcome>.Failure(preparation.Error!);
            if (preparation.Value!.Outcome is { } known)
                return known;
            var candidate = preparation.Value.Value!;
            if (candidate.Stop is { } stop)
                return stop;
            try
            {
                return await executor.ExecuteAsync<TAggregate, ReceiverOutcome>(create(), aggregate =>
                {
                    var accepted = acceptance(aggregate);
                    if (Decide(accepted, binding, aggregate.Stream) is { } nativeReceipt)
                        return AggregateOutcome.Discard(nativeReceipt);
                    var sameCut = candidate.Cut.Require(aggregate);
                    if (!sameCut.IsSuccess)
                        return AggregateOutcome.Discard(Result<ReceiverOutcome>.Failure(sameCut.Error!));
                    if (early(aggregate, candidate.Dependency) is { } executionStop)
                        return AggregateOutcome.Discard(executionStop);
                    var applied = operate(aggregate, candidate.Dependency);
                    if (!applied.IsSuccess)
                        return AggregateOutcome.Discard(Result<ReceiverOutcome>.Failure(applied.Error!));
                    return AggregateOutcome.Commit(Result<ReceiverOutcome>.Success(new ReceiverOutcome.Applied(
                        NativeFactReference.Of(aggregate.Stream, accepted.SelectedFact
                            ?? throw new InvalidOperationException("The receiver raised no fact for its logical id.")))));
                }, context, ct).ConfigureAwait(false);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // Unknown append outcomes and equal OCC winners rehydrate before any later target preparation.
            }
            catch (EventStreamConcurrencyException)
            {
                var current = await reader.HydrateAsync(create(), ct).ConfigureAwait(false);
                return Decide(acceptance(current), binding, current.Stream) ??
                    Wait("The target changed while the receiver applied; it will be retried.");
            }
        }
    }

    internal static Result<ReceiverOutcome>? Decide(NativeAcceptance acceptance, AcceptedOperationBinding binding,
        EventStreamAddress stream) => acceptance.Decide(binding) switch
        {
            NativeAcceptanceDecision.Replayed => Result<ReceiverOutcome>.Success(
                new ReceiverOutcome.Replayed(NativeFactReference.Of(stream, acceptance.SelectedFact!))),
            NativeAcceptanceDecision.Conflict => Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError),
            _ => null,
        };

    sealed record ReceiverCandidate<TPrepared>(IdentityOwnerPreparationCut Cut, TPrepared Dependency,
        Result<ReceiverOutcome>? Stop) where TPrepared : class;

    sealed class EmptyPreparation
    {
        public static EmptyPreparation Instance { get; } = new();
    }

    /// <summary>Parses an identity id from a process target key.</summary>
    public static Uuid TargetIdentity(string target) => Uuid.Parse(target, CultureInfo.InvariantCulture);
}

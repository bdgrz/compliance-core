using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     What every receiver of the administrator transition process shares: verifying the source proof and the
///     frozen intent against the command, deriving the receiver's operation provenance from them, and applying one
///     accepted fact on a target owner with replay, supersession and optimistic-concurrency handling.
/// </summary>
public sealed class AdministratorReceiverSupport(ISourceFactResolver resolver, IImmutableContentStore content,
    IAggregateExecutor executor, IAggregateReader reader, TenantManagerGuardLoader guards)
{
    const int MaximumAttempts = 5;

    /// <summary>Loads the tenant's guard with the contribution set its first fact pinned.</summary>
    public TenantManagerGuardLoader Guards => guards;

    /// <summary>The content store the receivers read their frozen intent from.</summary>
    public IImmutableContentStore Content => content;

    /// <summary>The reader the receivers use for the exact source owners they check.</summary>
    public IAggregateReader Reader => reader;

    /// <summary>The permanent error for a proof, intent or command that does not match the admission.</summary>
    public static Result<T> Invalid<T>(string message) where T : notnull =>
        Result<T>.Failure(new RequestError(RequestErrorKind.Conflict, message, false));

    /// <summary>The permanent failure for a proof, intent or command that does not match the admission.</summary>
    public static Result InvalidResult(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message, false));

    /// <summary>The transient error for a dependency that is not there yet.</summary>
    public static Result<ReceiverOutcome> Wait(string message) =>
        Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, message, true));

    /// <summary>A terminal no-op because a proven later episode or generation replaced this step.</summary>
    public static Result<ReceiverOutcome> Superseded(string reason) =>
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Superseded(reason, null));

    /// <summary>
    ///     Verifies the guard fact behind an apply receiver and its frozen intent against the command: the proof
    ///     re-reads the exact record, and operation, generation, owner, intent, member and episode must all agree.
    /// </summary>
    public async Task<Result<GuardAdmissionProof>> VerifyAdmissionAsync<TFact>(SourceFactProof<TFact> proof,
        IAdministratorTransitionReceiver request, Uuid memberEpisode, Func<AdministratorTransitionKind, bool> accepts,
        CancellationToken ct) where TFact : DomainEvent, ITenantManagerGuardFact
    {
        ArgumentNullException.ThrowIfNull(proof);
        VerifiedSourceProof<TFact> verified;
        try
        {
            verified = await resolver.VerifyAsync(proof, ct).ConfigureAwait(false);
        }
        catch (SourceFactVerificationException exception)
        {
            return Invalid<GuardAdmissionProof>($"The source proof does not verify: {exception.Message}");
        }

        var fact = verified.Facts;
        if (verified.Source.Realm != request.Target.Realm || fact.Operation.RootOperationId != request.OperationId ||
            fact.Operation.LogicalId != request.OperationId || fact.Generation != request.Generation ||
            fact.Intent != request.Intent || fact.Owner != request.Target)
            return Invalid<GuardAdmissionProof>("The guard fact does not carry this operation, generation and intent.");
        var intent = await ReadIntentAsync(request, memberEpisode, ct).ConfigureAwait(false);
        if (!intent.IsSuccess)
            return Result<GuardAdmissionProof>.Failure(intent.Error!);
        if (!accepts(intent.Value!.TransitionKind) || (long)intent.Value.GuardGeneration != fact.Generation)
            return Invalid<GuardAdmissionProof>("The intent does not match the admitted transition.");
        return Result<GuardAdmissionProof>.Success(new GuardAdmissionProof(fact, intent.Value, verified.EventId));
    }

    /// <summary>Re-reads the record behind a completion's source proof; any mismatch is a permanent conflict.</summary>
    public async Task<Result<VerifiedSourceProof<TFact>>> VerifySourceAsync<TFact>(SourceFactProof<TFact> proof,
        CancellationToken ct) where TFact : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(proof);
        try
        {
            return Result<VerifiedSourceProof<TFact>>.Success(await resolver.VerifyAsync(proof, ct).ConfigureAwait(false));
        }
        catch (SourceFactVerificationException exception)
        {
            return Result<VerifiedSourceProof<TFact>>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The source proof does not verify: {exception.Message}", false));
        }
    }

    /// <summary>Reads and checks the frozen intent a receiver command names.</summary>
    public async Task<Result<AdministratorTransitionIntent>> ReadIntentAsync(IAdministratorTransitionReceiver request,
        Uuid memberEpisode, CancellationToken ct)
    {
        AdministratorTransitionIntent intent;
        try
        {
            intent = await content.ReadJsonAsync<AdministratorTransitionIntent>(request.Target.Realm, request.Intent, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Invalid<AdministratorTransitionIntent>($"The frozen intent does not verify: {exception.Message}");
        }

        return intent.Header.RootOperationId != request.OperationId || intent.MemberId != request.MemberId ||
               intent.MemberEpisodeId != memberEpisode
            ? Invalid<AdministratorTransitionIntent>("The intent names another operation, member or episode.")
            : Result<AdministratorTransitionIntent>.Success(intent);
    }

    /// <summary>The provenance of a receiver's accepted fact on <paramref name="owner" />.</summary>
    public static OperationProvenance Provenance(IAdministratorTransitionReceiver request, string commandKind,
        EventStreamAddress owner, AdministratorTransitionIntent intent, Uuid causationEventId) =>
        new(request.CommandId, request.OperationId, causationEventId, commandKind, intent.Header.OriginalActor, owner,
            intent.MemberEpisodeId.ToString(), request.ExpectedRevision is >= 0 and var revision ? (ulong)revision : null,
            intent.Header.AuthoredIntentSha256, intent.Header.AcceptedAt, owner, owner, FirstOwner: intent.Header.FirstOwner);

    /// <summary>The acting member recorded by the original admission.</summary>
    public static Uuid ActorMemberId(AdministratorTransitionIntent intent) =>
        Uuid.TryParse(intent.Header.OriginalActor.Id, System.Globalization.CultureInfo.InvariantCulture, out var id)
            ? id
            : Uuid.Empty;

    /// <summary>The tenant a receiver acts for, from the realm of its target.</summary>
    public static Uuid TenantOf(EventStreamAddress target) =>
        Uuid.Parse(target.Realm, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    ///     Accepts one fact on the target owner. Replays return the original fact, conflicting reuse of the logical
    ///     id fails permanently, <paramref name="early" /> may stop with a wait or a supersession, and a lost append
    ///     race is retried against the rehydrated owner.
    /// </summary>
    public async ValueTask<Result<ReceiverOutcome>> ApplyAsync<TAggregate>(Func<TAggregate> create,
        Func<TAggregate, NativeAcceptance> acceptance, AcceptedOperationBinding binding,
        Func<TAggregate, Result<ReceiverOutcome>?> early, Func<TAggregate, Result> operate, IExecutionContext context,
        CancellationToken ct) where TAggregate : Aggregate
    {
        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await executor.ExecuteAsync<TAggregate, ReceiverOutcome>(create(), aggregate =>
                {
                    var accepted = acceptance(aggregate);
                    switch (accepted.Decide(binding))
                    {
                        case NativeAcceptanceDecision.Replayed:
                            return AggregateOutcome.Discard(Result<ReceiverOutcome>.Success(
                                new ReceiverOutcome.Replayed(NativeFactReference.Of(aggregate.Stream,
                                    accepted.SelectedFact!))));
                        case NativeAcceptanceDecision.Conflict:
                            return AggregateOutcome.Discard(Result<ReceiverOutcome>.Failure(
                                AcceptedAppendResult.ConflictError));
                    }

                    if (early(aggregate) is { } stop)
                        return AggregateOutcome.Discard(stop);
                    var applied = operate(aggregate);
                    if (!applied.IsSuccess)
                        return AggregateOutcome.Discard(Result<ReceiverOutcome>.Failure(applied.Error!));
                    return AggregateOutcome.Commit(Result<ReceiverOutcome>.Success(new ReceiverOutcome.Applied(
                        NativeFactReference.Of(aggregate.Stream, accepted.SelectedFact
                            ?? throw new InvalidOperationException("The receiver raised no fact for its logical id.")))));
                }, context, ct).ConfigureAwait(false);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The append lost the owner's OCC: rehydrate and decide again by logical id.
            }
            catch (EventStreamConcurrencyException)
            {
                return Wait("The target changed while the receiver applied; it will be retried.");
            }
        }
    }
}

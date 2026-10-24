using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Delivers one email challenge under the accepted at-least-once challenge policy and records every attempt as a
///     typed outcome command. An attempt is opened durably before the relay is called, so a crash between acceptance
///     and the outcome leaves an open attempt: the next run first closes it as an unknown outcome and only then opens
///     another, which resends the identical message. A definite rejection of 4xx kind and an unknown result are
///     retried within a bounded attempt budget; a 5xx rejection ends delivery. Digest delivery keeps its stricter
///     never-resend-after-unknown rule and is not served by this worker.
/// </summary>
/// <remarks>
///     The source fact is verified by hydrating the address stream it names: the fact must be the address's current
///     challenge fact of this challenge, at the stream, event id and revision the command carries. The mailbox is read
///     from that address, so the recipient never enters a command, an intent or a process record.
/// </remarks>
public sealed class EmailChallengeDeliveryWorker(IImmutableContentStore content, IAggregateReader reader,
    IEmailChallengeDeliveryLedger ledger, IEmailChallengeDelivery delivery, EmailChallengeTokenKeys tokenKeys,
    IRequestBus bus, TimeProvider clock)
{
    /// <summary>The realm of every challenge, delivery stream and frozen intent.</summary>
    public const string Realm = "bdgrz";

    /// <summary>The generation every delivery freezes.</summary>
    public const long FrozenGeneration = 1;

    /// <summary>Effect kind of an accepted attempt.</summary>
    public const string SentEffect = "record_email_challenge_delivery_sent";

    /// <summary>Effect kind of a failed attempt.</summary>
    public const string FailedEffect = "record_email_challenge_delivery_failed";

    /// <summary>Effect kind of the reconciliation that closes an attempt whose outcome was never recorded.</summary>
    public const string ReconcileEffect = "reconcile_email_challenge_delivery";

    /// <summary>The technical owner of one challenge's delivery.</summary>
    public static EventStreamAddress DeliveryStream(Uuid challengeId) =>
        new(Realm, "email-challenge-deliveries", challengeId.ToString());

    /// <summary>The deterministic command id of an outcome: one per source fact, effect and attempt ordinal, never the attempt id.</summary>
    public static Uuid OutcomeId(Uuid sourceEventId, string effectKind, Uuid challengeId, int attemptNumber) =>
        EffectIdentity.CommandId(sourceEventId, effectKind, challengeId.ToString(),
            attemptNumber.ToString(CultureInfo.InvariantCulture));

    /// <summary>Delivers a verification challenge.</summary>
    public async ValueTask<Result<ReceiverOutcome>> DeliverAsync(DeliverEmailChallenge request,
        IExecutionContext context, CancellationToken ct)
    {
        var address = await HydrateAsync(request.Source, ct).ConfigureAwait(false);
        if (!address.IsSuccess)
            return Result<ReceiverOutcome>.Failure(address.Error!);
        var owner = address.Value!;
        if (owner.CurrentIssuedFact is not { } fact || fact.ChallengeId != request.ChallengeId)
            return Replaced();
        var checkedFact = await CheckAsync(request, EmailChallengeDeliveryIntent.Verification, owner.Stream, fact,
            fact.ChallengeId, fact.UserId, fact.ExpiresAt, fact.TokenKeyId, fact.Operation.RootOperationId, ct)
            .ConfigureAwait(false);
        if (!checkedFact.IsSuccess)
            return Result<ReceiverOutcome>.Failure(checkedFact.Error!);

        var stale = owner.IsVerified || owner.CurrentChallengeId != fact.ChallengeId || clock.GetUtcNow() >= fact.ExpiresAt;
        return await RunAsync(request, context, request.Source.EventId, fact.ChallengeId, fact.ExpiresAt, fact.TokenHash,
            fact.TokenKeyId, EmailChallengeDeliveryIntent.Verification, stale,
            (keyId, expiresAt) => tokenKeys.TryDerive(keyId, fact.ChallengeId, fact.UserId, owner.Address!, expiresAt,
                out var token) ? token : null,
            (value, cancel) => delivery.SendAsync(fact.ChallengeId, fact.UserId, owner.Address!, value, cancel),
            ct).ConfigureAwait(false);
    }

    /// <summary>Delivers an identity recovery challenge.</summary>
    public async ValueTask<Result<ReceiverOutcome>> DeliverAsync(DeliverRecoveryChallenge request,
        IExecutionContext context, CancellationToken ct)
    {
        var address = await HydrateAsync(request.Source, ct).ConfigureAwait(false);
        if (!address.IsSuccess)
            return Result<ReceiverOutcome>.Failure(address.Error!);
        var owner = address.Value!;
        if (owner.CurrentRecoveryIssuedFact is not { } fact || fact.ChallengeId != request.ChallengeId)
            return Replaced();
        var checkedFact = await CheckAsync(request, EmailChallengeDeliveryIntent.Recovery, owner.Stream, fact,
            fact.ChallengeId, fact.UserId, fact.ExpiresAt, fact.TokenKeyId, fact.Operation.RootOperationId, ct)
            .ConfigureAwait(false);
        if (!checkedFact.IsSuccess)
            return Result<ReceiverOutcome>.Failure(checkedFact.Error!);

        var stale = !owner.IsVerified || owner.Owner != fact.UserId ||
                    owner.CurrentRecoveryChallengeId != fact.ChallengeId || owner.IsRecoveryCompleted ||
                    clock.GetUtcNow() >= fact.ExpiresAt;
        return await RunAsync(request, context, request.Source.EventId, fact.ChallengeId, fact.ExpiresAt, fact.TokenHash,
            fact.TokenKeyId, EmailChallengeDeliveryIntent.Recovery, stale,
            (keyId, expiresAt) => tokenKeys.TryDeriveRecovery(keyId, fact.ChallengeId, fact.UserId, owner.Address!,
                expiresAt, out var token) ? token : null,
            (value, cancel) => delivery.SendRecoveryAsync(fact.ChallengeId, fact.UserId, owner.Address!, value, cancel),
            ct).ConfigureAwait(false);
    }

    async ValueTask<Result<EmailAddress>> HydrateAsync(NativeFactReference source, CancellationToken ct)
    {
        if (source.Stream.Realm != Realm || source.Stream.Area != "email-addresses" ||
            !Uuid.TryParse(source.Stream.Resource, CultureInfo.InvariantCulture, out var addressId))
            return Invalid<EmailAddress>("The source fact is not on an email address stream.");
        var address = await reader.HydrateAsync(new EmailAddress(addressId, Uuid.Empty, null), ct).ConfigureAwait(false);
        return address.Address is null
            ? Invalid<EmailAddress>("The email address of the source fact does not exist.")
            : Result<EmailAddress>.Success(address);
    }

    async ValueTask<Result<EmailChallengeDeliveryIntent>> CheckAsync(IEmailChallengeDeliveryReceiver request,
        string purpose, EventStreamAddress stream, DomainEvent fact, Uuid factChallengeId, Uuid factUserId,
        DateTimeOffset factExpiresAt, string factKeyId, Uuid factRoot, CancellationToken ct)
    {
        if (NativeFactReference.Of(stream, fact) != request.Source || factChallengeId != request.ChallengeId ||
            factRoot != request.OperationId || request.OperationId != request.ChallengeId ||
            request.Generation != FrozenGeneration || request.Target != DeliveryStream(request.ChallengeId))
            return Invalid<EmailChallengeDeliveryIntent>(
                "The command does not name this challenge's accepted fact and its delivery owner.");
        EmailChallengeDeliveryIntent intent;
        try
        {
            intent = await content.ReadJsonAsync<EmailChallengeDeliveryIntent>(Realm, request.Intent, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Invalid<EmailChallengeDeliveryIntent>($"The frozen intent does not verify: {exception.Message}");
        }

        return intent.Header.RootOperationId != request.OperationId || intent.ChallengeId != request.ChallengeId ||
               intent.Purpose != purpose || intent.UserId != factUserId || intent.ExpiresAt != factExpiresAt ||
               intent.TokenKeyId != factKeyId
            ? Invalid<EmailChallengeDeliveryIntent>("The intent does not describe this challenge.")
            : Result<EmailChallengeDeliveryIntent>.Success(intent);
    }

    async ValueTask<Result<ReceiverOutcome>> RunAsync(IEmailChallengeDeliveryReceiver request,
        IExecutionContext context, Uuid sourceEventId, Uuid challengeId, DateTimeOffset expiresAt, string tokenHash,
        string tokenKeyId, string purpose, bool stale, Func<string, DateTimeOffset, string?> derive,
        Func<string, CancellationToken, ValueTask> send, CancellationToken ct)
    {
        _ = request;
        var record = await ledger.ReadAsync(challengeId, ct).ConfigureAwait(false);
        if (record is { IsDelivered: true })
            return Performed("challenge delivered");
        if (record is { IsFailed: true })
            return Performed("challenge delivery failed");
        if (stale)
            return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Superseded(
                "The challenge is no longer the current, unexpired challenge of its address.", null));

        var token = derive(tokenKeyId, expiresAt);
        if (token is null || !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(token)), Convert.FromHexString(tokenHash)))
        {
            // The key that derived the token is gone: no message can carry it. The challenge must be reissued.
            record = await ledger.OpenAttemptAsync(challengeId, purpose, clock.GetUtcNow(), ct).ConfigureAwait(false);
            if (record.Latest is { IsOpen: true } unusable)
            {
                var closed = await SendOutcomeAsync(context, new RecordEmailChallengeDeliveryFailed(
                    OutcomeId(sourceEventId, FailedEffect, challengeId, unusable.Number), challengeId, unusable.AttemptId,
                    EmailChallengeFailureCertainty.DefinitePermanent, "key_unavailable", clock.GetUtcNow()), ct)
                    .ConfigureAwait(false);
                if (!closed.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(closed.Error!);
            }

            return Performed("challenge key unavailable");
        }

        record = await ledger.ReadAsync(challengeId, ct).ConfigureAwait(false);
        if (record is { HasOpenAttempt: true, Latest: { } open })
        {
            // A previous run opened an attempt and never recorded how it ended, for example after a crash that
            // followed the relay's acceptance. Its outcome is unknown: close it as such before any resend.
            var reconciled = await SendOutcomeAsync(context, new RecordEmailChallengeDeliveryFailed(
                OutcomeId(sourceEventId, ReconcileEffect, challengeId, open.Number), challengeId, open.AttemptId,
                EmailChallengeFailureCertainty.Unknown, "outcome_not_recorded", clock.GetUtcNow()), ct)
                .ConfigureAwait(false);
            if (!reconciled.IsSuccess)
                return Result<ReceiverOutcome>.Failure(reconciled.Error!);
            record = await ledger.ReadAsync(challengeId, ct).ConfigureAwait(false);
            if (record is { IsFailed: true })
                return Performed("challenge delivery failed");
        }

        record = await ledger.OpenAttemptAsync(challengeId, purpose, clock.GetUtcNow(), ct).ConfigureAwait(false);
        if (record.Latest is not { IsOpen: true } attempt)
            return Performed(record.IsDelivered ? "challenge delivered" : "challenge delivery failed");

        try
        {
            await send(token, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failure = exception as EmailChallengeDeliveryException ??
                          new EmailChallengeDeliveryException(EmailChallengeFailureCertainty.Unknown,
                              "transport_ambiguous");
            var recorded = await SendOutcomeAsync(context, new RecordEmailChallengeDeliveryFailed(
                OutcomeId(sourceEventId, FailedEffect, challengeId, attempt.Number), challengeId, attempt.AttemptId,
                failure.Certainty, failure.FailureCode, clock.GetUtcNow()), ct).ConfigureAwait(false);
            if (!recorded.IsSuccess)
                return Result<ReceiverOutcome>.Failure(recorded.Error!);
            var after = await ledger.ReadAsync(challengeId, ct).ConfigureAwait(false);
            return after is { IsFailed: true }
                ? Performed("challenge delivery failed")
                : Wait("The challenge delivery attempt failed and will be retried.");
        }

        var sent = await SendOutcomeAsync(context, new RecordEmailChallengeDeliverySent(
            OutcomeId(sourceEventId, SentEffect, challengeId, attempt.Number), challengeId, attempt.AttemptId,
            clock.GetUtcNow()), ct).ConfigureAwait(false);
        return sent.IsSuccess ? Performed("challenge delivered") : Result<ReceiverOutcome>.Failure(sent.Error!);
    }

    async ValueTask<Result> SendOutcomeAsync(IExecutionContext context, IEmailChallengeDeliveryOutcome outcome,
        CancellationToken ct)
    {
        var result = await bus.SendAsync(outcome, context, ct).ConfigureAwait(false);
        return result.IsSuccess ? result : Result.Failure(result.Error!.IsTransient
            ? result.Error
            : new RequestError(RequestErrorKind.Conflict, result.Error.Message, false));
    }

    static Result<ReceiverOutcome> Replaced() => Result<ReceiverOutcome>.Success(new ReceiverOutcome.Superseded(
        "A newer challenge replaced this one on its address.", null));

    static Result<ReceiverOutcome> Performed(string effect) =>
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Performed(effect));

    static Result<ReceiverOutcome> Wait(string message) =>
        Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, message, true));

    static Result<T> Invalid<T>(string message) where T : notnull =>
        Result<T>.Failure(new RequestError(RequestErrorKind.Conflict, message, false));
}

using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Admits an identity recovery on the email address, which is its first owner. Both proofs are authenticated
///     first: the challenge token on the address and the replacement provider identity of the caller. The exact
///     candidate is frozen into <see cref="IdentityRecoveryChallengeClaimed" />, the swap then completes
///     automatically through the identity recovery process, and the answer is the fixed 202 acknowledgment that every
///     exact retry replays, even after the challenge expired or the recovery completed.
/// </summary>
public sealed class CompleteIdentityRecoveryHandler(
    NativeOperationExecutor operations,
    IAggregateReader reader,
    IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<CompleteIdentityRecovery, OperationAcceptedResponse>
{
    const string CommandKind = nameof(CompleteIdentityRecovery);
    const string Realm = IdentityRecoveryReceiverSupport.Realm;

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<CompleteIdentityRecovery> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.ChallengeId == Uuid.Empty || request.OldIdentityId == Uuid.Empty)
            return Fail(RequestErrorKind.Validation, "A challenge id and an old identity are required.");
        if (!EmailAddresses.TryNormalize(request.EmailAddress, out var normalized))
            return Fail(RequestErrorKind.Validation, "Enter a valid email address.");
        if (!OidcProviderClaims.TryGet(context.Actor, out var provider, out var subject, out var assertedEmail))
            return Fail(RequestErrorKind.Unauthorized,
                "Completing identity recovery requires proof of the replacement provider identity.");

        var now = clock.GetUtcNow();
        var replacementId = UserIdentity.GetIdentityId(provider, subject);
        var address = await reader.HydrateAsync(new EmailAddress(normalized, request.ChallengeId, CommandKind), ct)
            .ConfigureAwait(false);
        if (!address.IsVerified || address.Owner is not { } userId)
            return Fail(RequestErrorKind.Forbidden, "No verified recovery address is available.");

        var actor = new ActorReference("user", userId.ToString(), replacementId.ToString());
        var digest = AuthoredIntentDigest.Compute(new IdentityRecoveryAuthoredIntent(normalized, request.ChallengeId,
            request.OldIdentityId, provider, subject));
        var provenance = new OperationProvenance(request.ChallengeId, request.ChallengeId, null, CommandKind, actor,
            address.Stream, request.ChallengeId.ToString(), null, digest, now, address.Stream, address.Stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        if (address.Acceptance.Decide(binding) != NativeAcceptanceDecision.None)
        {
            // An exact retry replays the original acknowledgment before any proof or current state is consulted;
            // a changed intent under the same challenge conflicts.
            var replay = await operations.ExecuteAsync(new NativeOperation<EmailAddress>(binding,
                () => new EmailAddress(normalized, request.ChallengeId, CommandKind),
                current => current.Acceptance,
                _ => Result<FirstResponseDescriptor>.Failure(AcceptedAppendResult.ConflictError),
                (_, _) => Result.Failure(AcceptedAppendResult.ConflictError)), context, ct).ConfigureAwait(false);
            return await ReadAsync(replay, ct).ConfigureAwait(false);
        }

        var checks = await CheckAsync(request, userId, provider, subject, replacementId, ct).ConfigureAwait(false);
        if (!checks.IsSuccess)
            return Result<OperationAcceptedResponse>.Failure(checks.Error!);
        var proof = address.PrepareClaimIdentityRecovery(userId, request.ChallengeId, request.Token,
            request.OldIdentityId, replacementId, now);
        if (!proof.IsSuccess)
            return Result<OperationAcceptedResponse>.Failure(proof.Error!);

        // The frozen content is written before the append: the candidate intent, the two non-bearer proof
        // provenances and the fixed acknowledgment. None of it contains the token.
        var emailProof = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(), IdentityRecoveryProofProvenance.Schema,
            new IdentityRecoveryProofProvenance("email_challenge", request.ChallengeId, normalized,
                address.RecoveryTokenKeyId, null, now), ct).ConfigureAwait(false);
        var replacementProof = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(),
            IdentityRecoveryProofProvenance.Schema,
            new IdentityRecoveryProofProvenance("oidc_provider_identity", request.ChallengeId, replacementId.ToString(),
                null, assertedEmail, now), ct).ConfigureAwait(false);
        var oldIdentity = await reader.HydrateAsync(new UserIdentity(request.OldIdentityId), ct).ConfigureAwait(false);
        var header = new DurableIntentHeader(request.ChallengeId, address.Stream, address.Stream, address.Stream, actor,
            request.ChallengeId.ToString(), null, digest, now);
        var intent = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(), IdentityRecoveryIntent.Schema,
            new IdentityRecoveryIntent(header, request.ChallengeId, userId, request.OldIdentityId, replacementId,
                oldIdentity.Provider ?? string.Empty, oldIdentity.Identifier ?? string.Empty, provider, subject,
                emailProof, replacementProof), ct).ConfigureAwait(false);
        var acknowledgment = new OperationAcceptedResponse(request.ChallengeId,
            $"/api/v1/platform/operations/{request.ChallengeId}", now);
        var body = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(), "operation-accepted-response.v1",
            acknowledgment, ct).ConfigureAwait(false);
        var response = new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType,
            "operation-accepted-response.v1", new StandardResponseBody(body));

        var accepted = await operations.ExecuteAsync(new NativeOperation<EmailAddress>(binding,
            () => new EmailAddress(normalized, request.ChallengeId, CommandKind),
            current => current.Acceptance,
            current => current.PrepareClaimIdentityRecovery(userId, request.ChallengeId, request.Token,
                request.OldIdentityId, replacementId, now) is { IsSuccess: false } failed
                ? Result<FirstResponseDescriptor>.Failure(failed.Error!)
                : Result<FirstResponseDescriptor>.Success(response),
            (EmailAddress current, ImmutableContentRef frozen) => current.ClaimIdentityRecovery(provenance, intent,
                frozen, actor, emailProof, replacementProof, now, userId, request.ChallengeId, request.Token,
                request.OldIdentityId, replacementId, now)), context, ct).ConfigureAwait(false);
        return await ReadAsync(accepted, ct).ConfigureAwait(false);
    }

    /// <summary>The old identity must belong to the verified account; the replacement may be new or the same account's.</summary>
    async Task<Result> CheckAsync(CompleteIdentityRecovery request, Uuid userId, string provider, string subject,
        Uuid replacementId, CancellationToken ct)
    {
        if (request.OldIdentityId == replacementId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A different old identity is required."));
        var replacement = await reader.HydrateAsync(new UserIdentity(provider, subject), ct).ConfigureAwait(false);
        if (replacement.IsRegistered && (replacement.UserId != userId || replacement.IsRevoked))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The replacement identity is already registered or revoked."));
        var retired = await reader.HydrateAsync(new UserIdentity(request.OldIdentityId), ct).ConfigureAwait(false);
        return !retired.IsRegistered || retired.UserId != userId
            ? Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The old identity is not linked to this user."))
            : Result.Success;
    }

    async Task<Result<OperationAcceptedResponse>> ReadAsync(Result<AcceptedAppendResult> accepted, CancellationToken ct)
    {
        if (!accepted.IsSuccess)
            return Result<OperationAcceptedResponse>.Failure(accepted.Error!);
        var descriptor = accepted.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null,
        };
        if (descriptor is null)
            return Result<OperationAcceptedResponse>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body || descriptor.ResultSchema != "operation-accepted-response.v1")
            throw new ImmutableContentIntegrityException("The accepted recovery froze an unexpected response.");
        return Result<OperationAcceptedResponse>.Success(await content
            .ReadJsonAsync<OperationAcceptedResponse>(Realm, body.Content, ct).ConfigureAwait(false));
    }

    static Result<OperationAcceptedResponse> Fail(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}

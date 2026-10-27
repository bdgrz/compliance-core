using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

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
        var address = await reader.HydrateAsync(new EmailAddress(normalized, request.ChallengeId, CommandKind, request.ChallengeId), ct)
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
                () => new EmailAddress(normalized, request.ChallengeId, CommandKind, request.ChallengeId),
                current => current.Acceptance,
                _ => Result<FirstResponseDescriptor>.Failure(AcceptedAppendResult.ConflictError),
                (_, _) => Result.Failure(AcceptedAppendResult.ConflictError)), context, ct).ConfigureAwait(false);
            return await ReadAsync(replay, ct).ConfigureAwait(false);
        }

        var preparation = new IdentityOwnerPreparation(address);
        RecoveryAdmission? admission = null;
        var accepted = await operations.ExecuteAsync(new NativeOperation<EmailAddress>(binding,
            () => new EmailAddress(normalized, request.ChallengeId, CommandKind, request.ChallengeId),
            current => current.Acceptance,
            async (current, cancel) =>
            {
                // Receipt knowledge precedes mutable identity reads and every immutable candidate write.
                var same = preparation.Check(current);
                if (!same.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(same.Error!);
                var proof = current.PrepareClaimIdentityRecovery(userId, request.ChallengeId, request.Token,
                    request.OldIdentityId, replacementId, now);
                if (!proof.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(proof.Error!);
                var checkedIdentity = await CheckAsync(request, userId, provider, subject, replacementId, cancel)
                    .ConfigureAwait(false);
                if (!checkedIdentity.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(checkedIdentity.Error!);
                var oldIdentity = checkedIdentity.Value!;
                var emailProof = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(), IdentityRecoveryProofProvenance.Schema,
                    new IdentityRecoveryProofProvenance("email_challenge", request.ChallengeId, normalized,
                        current.RecoveryTokenKeyId, null, now), cancel).ConfigureAwait(false);
                var replacementProof = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(), IdentityRecoveryProofProvenance.Schema,
                    new IdentityRecoveryProofProvenance("oidc_provider_identity", request.ChallengeId, replacementId.ToString(),
                        null, assertedEmail, now), cancel).ConfigureAwait(false);
                var header = new DurableIntentHeader(request.ChallengeId, current.Stream, current.Stream, current.Stream, actor,
                    request.ChallengeId.ToString(), null, digest, now);
                var intent = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(), IdentityRecoveryIntent.Schema,
                    new IdentityRecoveryIntent(header, request.ChallengeId, userId, request.OldIdentityId, replacementId,
                        oldIdentity.Provider ?? string.Empty, oldIdentity.Identifier ?? string.Empty, provider, subject,
                        emailProof, replacementProof), cancel).ConfigureAwait(false);
                var acknowledgment = new OperationAcceptedResponse(request.ChallengeId,
                    $"/api/v1/platform/operations/{request.ChallengeId}", now);
                var body = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(), "operation-accepted-response.v1",
                    acknowledgment, cancel).ConfigureAwait(false);
                admission = new RecoveryAdmission(intent, emailProof, replacementProof);
                return preparation.Prepare(current, Result.Success,
                    new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType,
                        "operation-accepted-response.v1", new StandardResponseBody(body)));
            },
            (EmailAddress current, ImmutableContentRef frozen) =>
            {
                var candidate = admission ?? throw new InvalidOperationException("Recovery admission has no prepared proofs.");
                return preparation.Apply(current, () => current.ClaimIdentityRecovery(provenance, candidate.Intent, frozen,
                    actor, candidate.EmailProof, candidate.ReplacementProof, now, userId, request.ChallengeId,
                    request.Token, request.OldIdentityId, replacementId, now));
            }), context, ct).ConfigureAwait(false);
        return await ReadAsync(accepted, ct).ConfigureAwait(false);
    }

    /// <summary>The old identity belongs to the verified account; the replacement may be new or the same account's.</summary>
    async Task<Result<UserIdentity>> CheckAsync(CompleteIdentityRecovery request, Uuid userId, string provider, string subject,
        Uuid replacementId, CancellationToken ct)
    {
        if (request.OldIdentityId == replacementId)
            return Result<UserIdentity>.Failure(new RequestError(RequestErrorKind.Validation, "A different old identity is required."));
        var replacement = await reader.HydrateAsync(new UserIdentity(provider, subject), ct).ConfigureAwait(false);
        if (replacement.IsRegistered && (replacement.UserId != userId || replacement.IsRevoked))
            return Result<UserIdentity>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The replacement identity is already registered or revoked."));
        var retired = await reader.HydrateAsync(new UserIdentity(request.OldIdentityId), ct).ConfigureAwait(false);
        return !retired.IsRegistered || retired.UserId != userId
            ? Result<UserIdentity>.Failure(new RequestError(RequestErrorKind.Forbidden, "The old identity is not linked to this user."))
            : Result<UserIdentity>.Success(retired);
    }

    sealed record RecoveryAdmission(ImmutableContentRef Intent, ImmutableContentRef EmailProof, ImmutableContentRef ReplacementProof);

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

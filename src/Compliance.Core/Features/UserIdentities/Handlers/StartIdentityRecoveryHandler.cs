using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Starts recovery for a verified address. The challenge identity is the caller's, an exact retry replays the
///     original result and a changed address conflicts on the same logical identity. An unknown or unverified
///     address, and an address with an outstanding challenge, answer the same success without a fact, so the
///     response never reveals whether the address exists.
/// </summary>
public sealed class StartIdentityRecoveryHandler(
    NativeOperationExecutor operations,
    IAggregateReader reader,
    IEmailChallengeDeliveryLedger deliveries,
    EmailChallengeTokenKeys tokenKeys,
    IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<StartIdentityRecovery>
{
    const string CommandKind = nameof(StartIdentityRecovery);

    public async ValueTask<Result> HandleAsync(IRequestContext<StartIdentityRecovery> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.ChallengeId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A challenge id is required."));
        if (!EmailAddresses.TryNormalize(request.EmailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Enter a valid email address."));

        var now = clock.GetUtcNow();
        var current = await reader.HydrateAsync(new EmailAddress(normalized, request.ChallengeId, CommandKind, request.ChallengeId), ct)
            .ConfigureAwait(false);
        var provenance = Provenance(request, normalized, current.Stream, now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var decided = current.Acceptance.Decide(binding);
        // A challenge whose delivery ended in a definite failure no longer blocks a new one; the delivery process
        // owns that technical state, not the address.
        var deliveryFailed = current.CurrentRecoveryChallengeId is { } outstanding &&
                             await deliveries.ReadAsync(outstanding, ct).ConfigureAwait(false) is { IsFailed: true };
        if (decided == NativeAcceptanceDecision.None &&
            (current.Owner is null || !current.IsVerified || current.HasOutstandingRecoveryChallenge(now, deliveryFailed)))
            return Result.Success;

        var owner = current.Owner;
        var expiresAt = now.AddMinutes(15);
        var hash = string.Empty;
        if (decided == NativeAcceptanceDecision.None)
        {
            var token = tokenKeys.DeriveRecovery(tokenKeys.ActiveKeyId, request.ChallengeId, owner!.Value, normalized,
                expiresAt);
            hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        }

        var preparation = new IdentityOwnerPreparation(current);
        var accepted = await operations.ExecuteAsync(new NativeOperation<EmailAddress>(binding,
            () => new EmailAddress(normalized, request.ChallengeId, CommandKind, request.ChallengeId),
            address => address.Acceptance,
            (address, cancel) => preparation.PrepareAsync(address,
                address.PrepareIssueRecoveryChallenge(address.Owner ?? Uuid.Empty, request.ChallengeId, hash,
                    expiresAt, now, tokenKeys.ActiveKeyId, deliveryFailed),
                bodyCancel => EmailAddressOperations.UnitResponseAsync(content, bodyCancel), cancel),
            (EmailAddress address, ImmutableContentRef frozen) => preparation.Apply(address,
                () => address.IssueRecoveryChallenge(provenance, frozen, address.Owner ?? Uuid.Empty, request.ChallengeId,
                    hash, expiresAt, now, tokenKeys.ActiveKeyId, deliveryFailed))),
            context, ct).ConfigureAwait(false);
        if (!accepted.IsSuccess)
            return accepted.Error == EmailAddress.OutstandingRecoveryChallenge
                ? Result.Success
                : Result.Failure(accepted.Error!);
        return accepted.Value switch
        {
            AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed => Result.Success,
            _ => Result.Failure(AcceptedAppendResult.ConflictError),
        };
    }

    /// <summary>The provenance of an issuance; the binding of a retry is derived from it.</summary>
    static OperationProvenance Provenance(StartIdentityRecovery request, string normalizedAddress,
        EventStreamAddress owner, DateTimeOffset acceptedAt) =>
        new(request.ChallengeId, request.ChallengeId, null, CommandKind, Anonymous, owner,
            request.ChallengeId.ToString(), null,
            AuthoredIntentDigest.Compute(request with { EmailAddress = normalizedAddress }), acceptedAt, owner, owner);

    /// <summary>Recovery starts are unauthenticated by design; the verified address is the only proof.</summary>
    static ActorReference Anonymous { get; } = new("anonymous", "anonymous", "Anonymous recovery request");
}

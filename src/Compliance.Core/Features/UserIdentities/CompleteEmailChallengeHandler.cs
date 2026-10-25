using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Verifies the address with the token of the challenge. The challenge identity is the caller's and the logical
///     identity of the verification: an exact retry replays the original acceptance after the token is checked again,
///     and the bearer token never enters the fact or the intent digest.
/// </summary>
public sealed class CompleteEmailChallengeHandler(
    NativeOperationExecutor operations,
    IAggregateReader reader,
    IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<CompleteEmailChallenge>
{
    const string CommandKind = nameof(CompleteEmailChallenge);

    public async ValueTask<Result> HandleAsync(IRequestContext<CompleteEmailChallenge> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!EmailAddresses.TryNormalize(request.EmailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Enter a valid email address."));
        if (request.ChallengeId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A challenge id is required."));
        if (EmailAddressOperations.ActorOf(context.Actor) is not { } actor)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The caller identity is not recognized."));

        var now = clock.GetUtcNow();
        var current = await reader.HydrateAsync(Hydrate(normalized, request), ct).ConfigureAwait(false);
        var provenance = EmailAddressOperations.Provenance(request.ChallengeId, CommandKind, actor, current.Stream, -1,
            AuthoredIntentDigest.Compute(new CompleteEmailChallengeAuthoredIntent(request.ChallengeId, request.UserId,
                normalized)), now);
        if (current.Acceptance.Decide(AcceptedOperationBinding.Of(provenance)) == NativeAcceptanceDecision.Replayed)
        {
            // The original acceptance stands, but a retry still proves possession of the token.
            var proof = current.PrepareReplayCompleteChallenge(request.UserId, request.ChallengeId, request.Token);
            if (!proof.IsSuccess)
                return proof;
        }

        var preparation = new IdentityOwnerPreparation(current);
        return EmailAddressOperations.Of(await operations.ExecuteAsync(new NativeOperation<EmailAddress>(
            AcceptedOperationBinding.Of(provenance), () => Hydrate(normalized, request), address => address.Acceptance,
            (address, cancel) => preparation.PrepareAsync(address,
                address.PrepareCompleteChallenge(request.UserId, request.ChallengeId, request.Token, now),
                bodyCancel => EmailAddressOperations.UnitResponseAsync(content, bodyCancel), cancel),
            (EmailAddress address, ImmutableContentRef frozen) => preparation.Apply(address,
                () => address.CompleteChallenge(provenance, frozen, request.UserId, request.ChallengeId, request.Token, now))), context, ct).ConfigureAwait(false));
    }

    static EmailAddress Hydrate(string normalized, CompleteEmailChallenge request) =>
        new(normalized, request.ChallengeId, CommandKind, request.ChallengeId);
}

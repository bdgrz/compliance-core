using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

/// <summary>
///     Issues a verification challenge. The challenge identity is the caller's: an exact retry replays the original
///     acceptance and a changed address or user on the same identity conflicts. The fact keeps only the token hash;
///     delivering the token is the email challenge delivery process, not part of this admission.
/// </summary>
public sealed class IssueEmailChallengeHandler(
    NativeOperationExecutor operations,
    EmailChallengeTokenKeys tokenKeys,
    IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<IssueEmailChallenge>
{
    const string CommandKind = nameof(IssueEmailChallenge);

    public async ValueTask<Result> HandleAsync(IRequestContext<IssueEmailChallenge> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!EmailAddresses.TryNormalize(request.EmailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Enter a valid email address."));
        if (request.ChallengeId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A challenge id is required."));
        if (EmailAddressOperations.ActorOf(context.Actor) is not { } actor)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The caller identity is not recognized."));

        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(15);
        var keyId = tokenKeys.ActiveKeyId;
        var token = tokenKeys.Derive(keyId, request.ChallengeId, request.UserId, normalized, expiresAt);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var owner = new EmailAddress(normalized).Stream;
        var provenance = EmailAddressOperations.Provenance(request.ChallengeId, CommandKind, actor, owner, -1,
            AuthoredIntentDigest.Compute(request with { EmailAddress = normalized }), now);
        var preparation = new IdentityOwnerPreparation();
        return EmailAddressOperations.Of(await operations.ExecuteAsync(new NativeOperation<EmailAddress>(
            AcceptedOperationBinding.Of(provenance), () => Hydrate(normalized, request), address => address.Acceptance,
            (address, cancel) => preparation.PrepareAsync(address,
                address.PrepareIssueChallenge(request.UserId, request.ChallengeId, hash, expiresAt, now, keyId),
                bodyCancel => EmailAddressOperations.UnitResponseAsync(content, bodyCancel), cancel),
            (EmailAddress address, ImmutableContentRef frozen) => preparation.Apply(address,
                () => address.IssueChallenge(provenance, frozen, request.UserId, request.ChallengeId, hash, expiresAt, now, keyId))), context, ct)
            .ConfigureAwait(false));
    }

    static EmailAddress Hydrate(string normalized, IssueEmailChallenge request) =>
        new(normalized, request.ChallengeId, CommandKind, request.ChallengeId);
}

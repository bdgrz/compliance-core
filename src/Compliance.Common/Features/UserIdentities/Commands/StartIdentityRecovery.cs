using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Starts recovery through a separately verified email channel. <paramref name="ChallengeId" /> is chosen by the
///     caller, is the logical identity of the issuance and is the challenge the recovery completes; an exact retry
///     replays the original result and the response never reveals whether the address exists.
/// </summary>
[Discriminator("bdgrz.user-identity.start-recovery", 1)]
public sealed record StartIdentityRecovery(Uuid ChallengeId, string EmailAddress)
    : IRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => ChallengeId;
}

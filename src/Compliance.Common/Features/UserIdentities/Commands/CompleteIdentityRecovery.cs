using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>
///     Presents both recovery proofs: the email challenge token and the replacement provider identity of the
///     authenticated caller. Acceptance is the claim of the challenge on the email address, which freezes the exact
///     replacement; the swap then completes automatically and the answer is the fixed 202 acknowledgment.
///     <paramref name="ChallengeId" /> is the logical identity and an exact retry replays the acknowledgment.
/// </summary>
[Discriminator("bdgrz.user-identity.complete-recovery", 1)]
public sealed record CompleteIdentityRecovery(
    string EmailAddress,
    Uuid ChallengeId,
    string Token,
    Uuid OldIdentityId) : IRequest<OperationAcceptedResponse>, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => ChallengeId;
}

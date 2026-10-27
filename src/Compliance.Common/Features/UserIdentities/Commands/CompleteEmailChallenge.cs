using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>Completes the verification challenge named by <paramref name="ChallengeId" />, which is also the logical identity.</summary>
[Discriminator("bdgrz.email-address.complete-challenge", 1)]
public sealed record CompleteEmailChallenge(Uuid ChallengeId, Uuid UserId, string EmailAddress, string Token)
    : IRequest, IEmailOwnershipRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => ChallengeId;
}

using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>Issues a verification challenge; <paramref name="ChallengeId" /> is the caller-chosen challenge and logical identity.</summary>
[Discriminator("bdgrz.email-address.issue-challenge", 1)]
public sealed record IssueEmailChallenge(Uuid ChallengeId, Uuid UserId, string EmailAddress)
    : IRequest, IEmailOwnershipRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => ChallengeId;
}

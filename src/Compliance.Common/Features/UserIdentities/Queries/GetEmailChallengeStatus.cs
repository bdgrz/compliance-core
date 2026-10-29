using Cntryl.Portia;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Queries;

public sealed record GetEmailChallengeStatus(Uuid UserId, string EmailAddress)
    : IRequest<EmailChallengeStatusView>, IEmailOwnershipRequest, ICallable;

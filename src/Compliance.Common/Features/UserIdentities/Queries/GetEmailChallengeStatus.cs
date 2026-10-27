using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Queries;

public sealed record GetEmailChallengeStatus(Uuid UserId, string EmailAddress)
    : IRequest<EmailChallengeStatusView>, IEmailOwnershipRequest, ICallable;

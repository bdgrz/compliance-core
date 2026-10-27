using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Queries;

[Discriminator("bdgrz.email-address.get", 1)]
public sealed record GetEmailAddress(Uuid UserId, string EmailAddress)
    : IRequest<EmailAddressView>, IEmailOwnershipRequest, ICallable;

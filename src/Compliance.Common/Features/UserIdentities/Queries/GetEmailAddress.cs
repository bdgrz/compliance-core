using Cntryl.Portia;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Queries;

[Discriminator("bdgrz.email-address.get", 1)]
public sealed record GetEmailAddress(Uuid UserId, string EmailAddress)
    : IRequest<EmailAddressView>, IEmailOwnershipRequest, ICallable;

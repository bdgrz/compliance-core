using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Queries;

[Discriminator("bdgrz.email-address.list", 1)]
public sealed record ListEmailAddresses(Uuid UserId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<EmailAddressView>>, IEmailOwnershipRequest, ICallable;

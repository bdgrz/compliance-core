using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Queries;

[Discriminator("bdgrz.email-address.list", 1)]
public sealed record ListEmailAddresses(Uuid UserId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<EmailAddressView>>, IEmailOwnershipRequest, ICallable;

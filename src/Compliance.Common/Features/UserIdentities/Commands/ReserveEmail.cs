using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>
///     Reserves an email address for a user. <paramref name="CommandId" /> is the logical identity and
///     <paramref name="ExpectedRevision" /> the revision of the address the caller observed; a negative revision is
///     unconstrained and reserved for system callers such as the registration reactor.
/// </summary>
[Discriminator("bdgrz.email-address.reserve", 1)]
public sealed record ReserveEmail(Uuid CommandId, Uuid UserId, string EmailAddress, long ExpectedRevision)
    : IRequest, IEmailOwnershipRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

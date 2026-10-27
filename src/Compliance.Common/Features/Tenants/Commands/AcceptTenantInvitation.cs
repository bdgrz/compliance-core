using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Tenants.Models;
namespace Bdgrz.Compliance.Features.Tenants.Commands;

/// <summary>
///     Accepts an invitation with its single-use token. <paramref name="InvitationId" /> is route-bound and the token
///     must resolve that same invitation. The token is a secret verification input that never enters the authored
///     intent. There is no expected revision: acceptance consumes a one-time token the invitee holds alone, and the
///     invitation lifecycle serializes it.
/// </summary>
[Discriminator("bdgrz.tenant-invitation.accept", 1)]
public sealed record AcceptTenantInvitation(Uuid InvitationId, Uuid TenantId, string EmailAddress, string Token)
    : IRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => TenantInvitationIds.AcceptanceOf(InvitationId);
}

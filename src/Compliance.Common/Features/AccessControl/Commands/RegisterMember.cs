using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Registers a tenant member. <paramref name="CommandId" /> is the logical identity of the registration and
///     becomes the membership episode id; an exact retry replays the original acceptance.
/// </summary>
[Discriminator("bdgrz.rbac.member.register", 1)]
public sealed record RegisterMember(Uuid CommandId, Uuid TenantId, Uuid UserId,
    string Affiliation = "client_personnel", DateOnly? MembershipExpiresOn = null)
    : IRequest, IRbacManagementRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

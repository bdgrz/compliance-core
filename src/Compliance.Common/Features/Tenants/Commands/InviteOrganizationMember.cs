using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Commands;

/// <summary>
///     Invites a person to an organization as client personnel with a built-in role. <paramref name="InvitationId" />
///     is the client-chosen identity of the invitation; an exact retry replays the original acceptance.
/// </summary>
[Discriminator("bdgrz.organization-member.invite", 1)]
public sealed record InviteOrganizationMember(Uuid InvitationId, Uuid TenantId, string EmailAddress,
    string BuiltInRole) : IRequest, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => InvitationId;
}

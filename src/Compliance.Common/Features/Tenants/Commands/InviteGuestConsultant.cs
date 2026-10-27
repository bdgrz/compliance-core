using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Invites a guest consultant to a fixed set of programs until <paramref name="ExpiresOn" />.
///     <paramref name="InvitationId" /> is the client-chosen identity of the invitation; an exact retry replays the
///     original acceptance.
/// </summary>
[Discriminator("bdgrz.tenant.guest-consultant.invite", 1)]
public sealed record InviteGuestConsultant(Uuid InvitationId, Uuid TenantId, string EmailAddress,
    IReadOnlyList<Uuid> ProgramIds, DateOnly ExpiresOn)
    : IRequest, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => InvitationId;
}

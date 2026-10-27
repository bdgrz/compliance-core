using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Terminates a member episode through the tenant manager guard. <paramref name="CommandId" /> is the logical
///     identity of the deprovisioning and an exact retry replays the original acceptance.
///     <paramref name="MemberEpisodeId" /> is the episode being terminated and
///     <paramref name="ExpectedMemberRevision" /> the member revision the caller observed; a negative revision is
///     unconstrained and reserved for system callers.
/// </summary>
[Discriminator("bdgrz.tenant.member.deprovision", 1)]
public sealed record DeprovisionMember(Uuid CommandId, Uuid TenantId, Uuid UserId, string Reason,
    Uuid MemberEpisodeId, long ExpectedMemberRevision)
    : IRequest<AdministratorTransitionResponse>, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

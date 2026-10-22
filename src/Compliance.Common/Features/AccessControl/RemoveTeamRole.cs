using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.team-role.remove", 1)]
public sealed record RemoveTeamRole(Uuid TenantId, Uuid TeamId, Uuid RoleId,
    Uuid CommandId, ulong ExpectedRevision, Uuid AssignmentId)
    : IRequest, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

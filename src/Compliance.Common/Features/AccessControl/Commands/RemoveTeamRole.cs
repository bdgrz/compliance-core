using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

[Discriminator("bdgrz.rbac.team-role.remove", 1)]
public sealed record RemoveTeamRole(Uuid TenantId, Uuid TeamId, Uuid RoleId,
    Uuid CommandId, ulong ExpectedRevision, Uuid AssignmentId, AccessGrantScope? Scope = null)
    : IRequest, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Deletes a team from a tenant. Built-in teams cannot be deleted. <paramref name="CommandId" /> is the logical
///     identity of the deletion and an exact retry replays the original acceptance.
/// </summary>
[Discriminator("bdgrz.rbac.team.delete", 1)]
public sealed record DeleteTeam(Uuid CommandId, Uuid TenantId, Uuid TeamId)
    : IRequest, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

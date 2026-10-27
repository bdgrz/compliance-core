using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Deletes a role from a tenant. Built-in roles cannot be deleted. <paramref name="CommandId" /> is the logical
///     identity of the deletion and an exact retry replays the original acceptance.
/// </summary>
[Discriminator("bdgrz.rbac.role.delete", 1)]
public sealed record DeleteRole(Uuid CommandId, Uuid TenantId, Uuid RoleId)
    : IRequest, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

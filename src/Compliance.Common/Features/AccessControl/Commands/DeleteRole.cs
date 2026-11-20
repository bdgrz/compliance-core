using Cntryl.Portia;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Deletes a role from a tenant. Built-in roles cannot be deleted. The role identity is also the stable
///     request identity, so retrying DELETE addresses the same deletion.
/// </summary>
[Discriminator("bdgrz.rbac.role.delete", 2)]
public sealed record DeleteRole(Uuid TenantId, Uuid RoleId)
    : IRequest, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    [JsonIgnore]
    public Uuid OperationId => Uuid.CreateVersion5(RoleId, $"role-delete:{TenantId}");

    Uuid IIdentifiedRequest.RequestId => OperationId;
}

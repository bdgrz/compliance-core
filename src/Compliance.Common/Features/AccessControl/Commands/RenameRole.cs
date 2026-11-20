using Cntryl.Portia;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Replaces a role's name at the revision the caller observed. The request identity is derived from the role and
///     expected revision, so an identical retry replays while a changed body at that revision conflicts.
/// </summary>
[Discriminator("bdgrz.rbac.role.rename", 2)]
public sealed record RenameRole(Uuid TenantId, Uuid RoleId, string Name, long ExpectedRevision)
    : IRequest, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    [JsonIgnore]
    public Uuid OperationId => Uuid.CreateVersion5(RoleId,
        $"role-rename:{TenantId}:{ExpectedRevision}");

    Uuid IIdentifiedRequest.RequestId => OperationId;
}

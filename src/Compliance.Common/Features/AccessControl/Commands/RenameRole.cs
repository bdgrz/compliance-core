using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Renames a role. <paramref name="CommandId" /> is the logical identity of the rename and an exact retry replays
///     the original acceptance; the same identity with a different name conflicts.
/// </summary>
[Discriminator("bdgrz.rbac.role.rename", 1)]
public sealed record RenameRole(Uuid CommandId, Uuid TenantId, Uuid RoleId, string Name)
    : IRequest, IRbacManagementRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

static class RbacPermissionEffects
{
    public static AssignRolePermission Assign(Uuid sourceEventId, Uuid tenantId, Uuid roleId, string permission)
    {
        var owner = RbacIds.RolePermission(tenantId, roleId, Permissions.Normalize(permission));
        var commandId = Uuid.CreateVersion5(sourceEventId, $"role-permission:assign:{owner}:initial");
        return new AssignRolePermission(tenantId, roleId, permission, commandId, 0);
    }

    public static RemoveRolePermission Remove(Uuid sourceEventId, Uuid tenantId, Uuid roleId,
        RolePermissionView permission)
    {
        if (sourceEventId == Uuid.Empty || tenantId == Uuid.Empty || roleId == Uuid.Empty || permission.RoleId != roleId ||
            permission.AssignmentId == Uuid.Empty || permission.Revision == 0 || string.IsNullOrWhiteSpace(permission.Permission))
            throw new InvalidOperationException("Permission cleanup requires the exact owner, assignment episode and source revision.");
        var owner = RbacIds.RolePermission(tenantId, roleId, Permissions.Normalize(permission.Permission));
        var commandId = Uuid.CreateVersion5(sourceEventId,
            $"role-permission:remove:{owner}:{permission.AssignmentId}");
        return new RemoveRolePermission(tenantId, roleId, permission.Permission, commandId, permission.Revision,
            permission.AssignmentId);
    }
}

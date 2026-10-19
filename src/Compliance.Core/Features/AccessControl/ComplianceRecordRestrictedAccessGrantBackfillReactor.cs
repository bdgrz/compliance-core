using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Replays tenant registrations to grant restricted compliance-record access to built-in leads.</summary>
public sealed partial class ComplianceRecordRestrictedAccessGrantBackfillReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants")),
      IReactorHandler<TenantRegistered>
{
    public async ValueTask HandleAsync(IReactorContext<TenantRegistered> context,
        CancellationToken ct)
    {
        var tenantId = context.Trigger.TenantId;
        foreach (var roleId in new[]
                 {
                     BuiltInRbac.TenantAdministrationRoleId(tenantId),
                     BuiltInRbac.ComplianceManagementRoleId(tenantId),
                 })
        {
            await bus.SendReactionAsync(new AssignRolePermission(tenantId, roleId,
                    RbacPermissions.ComplianceRecordRestrictedRead), context, ct)
                .ConfigureAwait(false);
            await bus.SendReactionAsync(new AssignRolePermission(tenantId, roleId,
                    RbacPermissions.ComplianceRecordRestrictedWrite), context, ct)
                .ConfigureAwait(false);
        }
    }
}

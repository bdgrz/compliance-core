using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class TenantRbacBootstrapReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants")),
      IReactorHandler<TenantRegistered>
{
    public async ValueTask HandleAsync(IReactorContext<TenantRegistered> context, CancellationToken ct)
    {
        var tenantId = context.Trigger.TenantId;
        // BuiltInRoleCatalogMigrationReactor owns all rbac-roles streams, including defining
        // current names for new registrations and renaming legacy roles. Feature-specific
        // permission backfills likewise own their individual role-permission streams.
        var ownerBecomesAdministrator = context.Trigger.CreatorIsAdministrator ||
                                        context.Trigger.FirstAdministratorEmail is null;
        List<IRequest> commands =
        [
            (new DefineTeam(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId),
                BuiltInRbac.AdministratorsTeamName)),
        ];

        // Let membership projection start as soon as the admin team exists. The admin role link
        // and core grants stay ahead of the assignment event that triggers tenant activation.
        if (ownerBecomesAdministrator)
        {
            commands.Add(new RegisterMember(tenantId, context.Trigger.OwnerUserId));
        }

        commands.Add(new AssignTeamRole(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId),
            BuiltInRbac.TenantAdministrationRoleId(tenantId)));
        commands.Add(new AssignRolePermission(tenantId, BuiltInRbac.TenantAdministrationRoleId(tenantId),
            RbacPermissions.TenantAccess));
        commands.Add(new AssignRolePermission(tenantId, BuiltInRbac.TenantAdministrationRoleId(tenantId),
            RbacPermissions.TenantRbacManage));
        commands.Add(new AssignRolePermission(tenantId, BuiltInRbac.TenantAdministrationRoleId(tenantId),
            RbacPermissions.ProgramManage));

        if (ownerBecomesAdministrator)
        {
            var memberId = RbacIds.Member(tenantId, context.Trigger.OwnerUserId);
            commands.Add(new AssignTeamMember(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId), memberId));
        }

        commands.AddRange(
        [
            new DefineTeam(tenantId, BuiltInRbac.PowerUsersTeamId(tenantId), BuiltInRbac.PowerUsersTeamName),
            new DefineTeam(tenantId, BuiltInRbac.StandardUsersTeamId(tenantId), BuiltInRbac.StandardUsersTeamName),
            new AssignTeamRole(tenantId, BuiltInRbac.PowerUsersTeamId(tenantId),
                BuiltInRbac.ComplianceManagementRoleId(tenantId)),
            new AssignTeamRole(tenantId, BuiltInRbac.StandardUsersTeamId(tenantId),
                BuiltInRbac.ComplianceParticipationRoleId(tenantId)),
            new AssignRolePermission(tenantId, BuiltInRbac.ComplianceManagementRoleId(tenantId),
                RbacPermissions.TenantAccess),
            new AssignRolePermission(tenantId, BuiltInRbac.ComplianceManagementRoleId(tenantId),
                RbacPermissions.ProgramManage),
            new AssignRolePermission(tenantId, BuiltInRbac.ComplianceParticipationRoleId(tenantId),
                RbacPermissions.TenantAccess),
        ]);

        foreach (var command in commands)
            await bus.SendReactionAsync(command, context, ct);
    }
}

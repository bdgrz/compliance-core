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
        var sourceEventId = context.Trigger.Metadata.EventId;
        var adminRoleId = BuiltInRbac.TenantAdministrationRoleId(tenantId);
        var ownerBecomesAdministrator = context.Trigger.CreatorIsAdministrator ||
                                        context.Trigger.FirstAdministratorEmail is null;
        List<IRequest> commands =
        [
            new DefineRole(tenantId, adminRoleId, BuiltInRbac.TenantAdministrationRoleName),
            new DefineTeam(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId), BuiltInRbac.AdministratorsTeamName),
        ];

        // Let membership projection start as soon as the admin team exists. The admin role link
        // and core grants stay ahead of the assignment event that triggers tenant activation.
        if (ownerBecomesAdministrator)
        {
            commands.Add(new RegisterMember(tenantId, context.Trigger.OwnerUserId));
        }

        commands.Add(RbacTeamRoleEffects.Assign(sourceEventId, tenantId, BuiltInRbac.AdministratorsTeamId(tenantId), adminRoleId));
        commands.Add(RbacPermissionEffects.Assign(sourceEventId, tenantId, adminRoleId, RbacPermissions.TenantAccess));
        commands.Add(RbacPermissionEffects.Assign(sourceEventId, tenantId, adminRoleId, RbacPermissions.TenantRbacManage));
        commands.Add(RbacPermissionEffects.Assign(sourceEventId, tenantId, adminRoleId, RbacPermissions.ProgramManage));

        if (ownerBecomesAdministrator)
        {
            var memberId = RbacIds.Member(tenantId, context.Trigger.OwnerUserId);
            commands.Add(new AssignTeamMember(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId), memberId));
        }

        commands.AddRange(
        [
            new DefineRole(tenantId, BuiltInRbac.ComplianceManagementRoleId(tenantId), BuiltInRbac.ComplianceManagementRoleName),
            new DefineRole(tenantId, BuiltInRbac.ComplianceParticipationRoleId(tenantId), BuiltInRbac.ComplianceParticipationRoleName),
            new DefineRole(tenantId, BuiltInRbac.ViewerRoleId(tenantId), BuiltInRbac.ViewerRoleName),
            new DefineTeam(tenantId, BuiltInRbac.PowerUsersTeamId(tenantId), BuiltInRbac.PowerUsersTeamName),
            new DefineTeam(tenantId, BuiltInRbac.StandardUsersTeamId(tenantId), BuiltInRbac.StandardUsersTeamName),
            new DefineTeam(tenantId, BuiltInRbac.ViewersTeamId(tenantId), BuiltInRbac.ViewersTeamName),
            RbacTeamRoleEffects.Assign(sourceEventId, tenantId, BuiltInRbac.PowerUsersTeamId(tenantId), BuiltInRbac.ComplianceManagementRoleId(tenantId)),
            RbacTeamRoleEffects.Assign(sourceEventId, tenantId, BuiltInRbac.StandardUsersTeamId(tenantId), BuiltInRbac.ComplianceParticipationRoleId(tenantId)),
            RbacTeamRoleEffects.Assign(sourceEventId, tenantId, BuiltInRbac.ViewersTeamId(tenantId), BuiltInRbac.ViewerRoleId(tenantId)),
        ]);
        commands.AddRange(BuiltInRolePermissions.Create(sourceEventId, tenantId)
            .Where(grant => grant.RoleId != adminRoleId || grant.Permission is not
                (RbacPermissions.TenantAccess or RbacPermissions.TenantRbacManage or RbacPermissions.ProgramManage)));

        foreach (var command in commands)
            await bus.SendReactionAsync(command, context, ct);
    }
}

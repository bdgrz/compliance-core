using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

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
        List<object> commands =
        [
            new DefineRole(tenantId, adminRoleId, BuiltInRbac.TenantAdministrationRoleName),
            new DefineTeam(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId), BuiltInRbac.AdministratorsTeamName),
        ];

        // Let membership projection start as soon as the admin team exists. The admin role link
        // and core grants stay ahead of the assignment event that triggers tenant activation.
        if (ownerBecomesAdministrator)
        {
            commands.Add(MemberRegistrationEffects.Register(sourceEventId, tenantId, context.Trigger.OwnerUserId));
        }

        commands.Add(RbacTeamRoleEffects.Assign(sourceEventId, tenantId, BuiltInRbac.AdministratorsTeamId(tenantId), adminRoleId));
        commands.Add(RbacPermissionEffects.Assign(sourceEventId, tenantId, adminRoleId, RbacPermissions.TenantAccess));
        commands.Add(RbacPermissionEffects.Assign(sourceEventId, tenantId, adminRoleId, RbacPermissions.TenantRbacManage));
        commands.Add(RbacPermissionEffects.Assign(sourceEventId, tenantId, adminRoleId, RbacPermissions.ProgramManage));

        if (ownerBecomesAdministrator)
        {
            var memberId = RbacIds.Member(tenantId, context.Trigger.OwnerUserId);
            commands.Add(TeamMemberEffects.Assign(sourceEventId, tenantId, BuiltInRbac.AdministratorsTeamId(tenantId), memberId));
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
        {
            if (command is AssignTeamMember assignment)
            {
                var assigned = await bus.SendAsync(assignment, context, ct).ConfigureAwait(false);
                if (!assigned.IsSuccess)
                    throw new ReactionCommandFailedException(assigned.Error);
            }
            else if (command is DefineRole or DefineTeam)
            {
                // A replayed bootstrap meets built-ins that are already defined. A definition the owner refuses
                // as conflicting content (a catalog name migrated by RenameRole since) must not block the
                // checkpoint: the accepted definition stays authoritative.
                var defined = await bus.SendAsync((IRequest)command, context, ct).ConfigureAwait(false);
                if (!defined.IsSuccess && defined.Error.Kind != RequestErrorKind.Conflict)
                    throw new ReactionCommandFailedException(defined.Error);
            }
            else
            {
                await bus.SendReactionAsync((IRequest)command, context, ct);
            }
        }
    }
}

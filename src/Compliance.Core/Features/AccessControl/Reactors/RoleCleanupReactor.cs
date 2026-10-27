using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Removes a deleted role's permission assignments and team assignments so no orphaned
///     <see cref="RolePermission" />/<see cref="TeamRole" /> assignment survives the role itself. Discovery is
///     source-complete: the live relationships come from the authoritative assignment facts, never from a directory
///     row, so a lagging or empty projector cannot hide one and the checkpoint cannot advance past an unseen one.
///     Every removal names the exact assignment episode and revision it closes; an assignment that a newer episode
///     replaced is left alone.
/// </summary>
public sealed partial class RoleCleanupReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus,
    IDomainEventReader events,
    IAggregateReader reader)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-roles"), "RoleCleanup"),
      IReactorHandler<RoleDeleted>
{
    public async ValueTask HandleAsync(IReactorContext<RoleDeleted> context, CancellationToken ct)
    {
        var tenantId = context.Trigger.TenantId;
        var roleId = context.Trigger.RoleId;
        var sourceEventId = context.Trigger.Metadata.EventId;

        foreach (var permission in await RbacRelationshipSource.LivePermissionsAsync(events, tenantId, roleId, ct)
                     .ConfigureAwait(false))
        {
            var removal = await bus.SendAsync(RbacPermissionEffects.Remove(sourceEventId, tenantId, roleId, permission),
                context, ct).ConfigureAwait(false);
            if (removal.IsSuccess)
                continue;
            var owner = await reader.HydrateAsync(new RolePermission(tenantId, roleId, permission.Permission), ct)
                .ConfigureAwait(false);
            if (owner.AssignmentId == permission.AssignmentId)
                throw new ReactionCommandFailedException(removal.Error);
        }

        foreach (var team in await RbacRelationshipSource.LiveTeamsAsync(events, tenantId, roleId, ct)
                     .ConfigureAwait(false))
        {
            var removal = await bus.SendAsync(RbacTeamRoleEffects.Remove(sourceEventId, tenantId, roleId, team),
                context, ct).ConfigureAwait(false);
            if (removal.IsSuccess)
                continue;
            var owner = await reader.HydrateAsync(new TeamRole(tenantId, team.TeamId, roleId), ct)
                .ConfigureAwait(false);
            if (owner.AssignmentId == team.AssignmentId)
                throw new ReactionCommandFailedException(removal.Error);
        }
    }
}

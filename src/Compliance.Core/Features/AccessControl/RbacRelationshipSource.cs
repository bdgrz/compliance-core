using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Enumerates the live relationships of a deleted role or team from the authoritative native facts. The scan
///     folds every fact of the tenant's relationship area from its start, so no directory or projection row decides
///     which relationship exists and a lagging or empty projector can never hide one. Each live relationship carries
///     the exact assignment episode and the owner revision of its last fact, so a removal can only close that
///     episode and never a newer assignment that follows it.
/// </summary>
static class RbacRelationshipSource
{
    /// <summary>The permissions of the role that are still assigned, in a stable order.</summary>
    public static async ValueTask<IReadOnlyList<RolePermissionView>> LivePermissionsAsync(IDomainEventReader events,
        Uuid tenantId, Uuid roleId, CancellationToken ct)
    {
        var live = new Dictionary<EventStreamAddress, RolePermissionView?>();
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString(), "rbac-role-permissions");
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            switch (record.Event)
            {
                case RolePermissionAssigned assigned when assigned.TenantId == tenantId && assigned.RoleId == roleId:
                    live[record.Stream] = new RolePermissionView(roleId, assigned.Permission, assigned.CommandId,
                        record.Event.Metadata.AggregateVersion);
                    break;
                case RolePermissionRemoved removed when removed.TenantId == tenantId && removed.RoleId == roleId:
                    live[record.Stream] = null;
                    break;
            }
        }

        return live.Values.OfType<RolePermissionView>()
            .OrderBy(static view => view.Permission, StringComparer.Ordinal).ToList();
    }

    /// <summary>The teams that still hold the role, in a stable order.</summary>
    public static async ValueTask<IReadOnlyList<RoleTeamView>> LiveTeamsAsync(IDomainEventReader events,
        Uuid tenantId, Uuid roleId, CancellationToken ct)
    {
        var live = new Dictionary<EventStreamAddress, RoleTeamView?>();
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString(), "rbac-team-roles");
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            switch (record.Event)
            {
                case TeamRoleAssigned assigned when assigned.TenantId == tenantId && assigned.RoleId == roleId:
                    live[record.Stream] = new RoleTeamView(roleId, assigned.TeamId, assigned.CommandId,
                        record.Event.Metadata.AggregateVersion);
                    break;
                case TeamRoleRemoved removed when removed.TenantId == tenantId && removed.RoleId == roleId:
                    live[record.Stream] = null;
                    break;
            }
        }

        return live.Values.OfType<RoleTeamView>().OrderBy(static view => view.TeamId.ToString(), StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The members that are still assigned to the team, with the exact episode of each assignment.</summary>
    public static async ValueTask<IReadOnlyList<TeamAssignmentSource>> LiveMembersAsync(IDomainEventReader events,
        Uuid tenantId, Uuid teamId, CancellationToken ct)
    {
        var live = new Dictionary<EventStreamAddress, TeamAssignmentSource?>();
        var generations = new Dictionary<EventStreamAddress, ulong>();
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString(), "rbac-team-members");
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            switch (record.Event)
            {
                case TeamMemberAssigned assigned when assigned.TenantId == tenantId && assigned.TeamId == teamId:
                    var generation = generations.GetValueOrDefault(record.Stream) + 1;
                    generations[record.Stream] = generation;
                    live[record.Stream] = new TeamAssignmentSource(assigned.MemberId, assigned.MembershipEpisodeId,
                        assigned.Metadata.EventId, generation, record.Event.Metadata.AggregateVersion);
                    break;
                case TeamMemberRemoved removed when removed.TenantId == tenantId && removed.TeamId == teamId:
                    live[record.Stream] = null;
                    break;
            }
        }

        return live.Values.OfType<TeamAssignmentSource>()
            .OrderBy(static source => source.MemberId.ToString(), StringComparer.Ordinal).ToList();
    }
}

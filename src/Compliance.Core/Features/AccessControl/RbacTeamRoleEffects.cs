using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

static class RbacTeamRoleEffects
{
    public static AssignTeamRole Assign(Uuid sourceEventId, Uuid tenantId, Uuid teamId, Uuid roleId)
    {
        var owner = RbacIds.TeamRole(tenantId, teamId, roleId);
        var commandId = Uuid.CreateVersion5(sourceEventId, $"team-role:assign:{owner}:initial");
        return new AssignTeamRole(tenantId, teamId, roleId, commandId, 0);
    }

    public static RemoveTeamRole Remove(Uuid sourceEventId, Uuid tenantId, Uuid roleId, RoleTeamView team)
    {
        if (sourceEventId == Uuid.Empty || tenantId == Uuid.Empty || roleId == Uuid.Empty || team.RoleId != roleId ||
            team.TeamId == Uuid.Empty || team.AssignmentId == Uuid.Empty || team.Revision == 0)
            throw new InvalidOperationException("Team role cleanup requires the exact owner, assignment episode and source revision.");
        var owner = RbacIds.TeamRole(tenantId, team.TeamId, roleId);
        var commandId = Uuid.CreateVersion5(sourceEventId, $"team-role:remove:{owner}:{team.AssignmentId}");
        return new RemoveTeamRole(tenantId, team.TeamId, roleId, commandId, team.Revision, team.AssignmentId);
    }
}

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

static class RbacTeamRoleEffects
{
    public static AssignTeamRole Assign(Uuid tenantId, Uuid teamId, Uuid roleId) =>
        new(tenantId, teamId, roleId, 0);

    public static RemoveTeamRole Remove(Uuid tenantId, Uuid roleId, RoleTeamView team)
    {
        if (tenantId == Uuid.Empty || roleId == Uuid.Empty || team.RoleId != roleId ||
            team.TeamId == Uuid.Empty || team.AssignmentId == Uuid.Empty || team.Revision == 0)
            throw new InvalidOperationException("Team role cleanup requires the exact owner, assignment episode and source revision.");
        return new RemoveTeamRole(tenantId, team.TeamId, roleId, team.Revision, team.Scope);
    }
}

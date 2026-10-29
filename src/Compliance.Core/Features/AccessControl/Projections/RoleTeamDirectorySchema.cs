using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>
///     The shared role-team-directory schema — the same <see cref="TeamRoleAssigned" />/
///     <see cref="TeamRoleRemoved" /> events as <c>rbac-team-roles</c>, materialized keyed by role
///     first instead of by team, so a role's detail page can list the teams holding it without a
///     substring-search fallback over an unrelated index.
/// </summary>
static class RoleTeamDirectorySchema
{
    public static readonly KvDirectoryIndex<RoleTeamView> ByRole = new(
        "by_role", 1, static view => [view.RoleId.ToString(), view.TeamId.ToString()]);

    public static readonly KvDirectory<RoleTeamView, (Uuid RoleId, Uuid TeamId)> Directory = new(
        "role-teams",
        ComplianceCoreJsonContext.Default.RoleTeamView,
        static view => (view.RoleId, view.TeamId),
        static identity => [identity.RoleId.ToString(), identity.TeamId.ToString()],
        [ByRole]);
}

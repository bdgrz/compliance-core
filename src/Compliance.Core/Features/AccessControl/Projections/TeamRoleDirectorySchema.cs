using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>
///     The team-role-directory schema — the same <see cref="TeamRoleAssigned" />/
///     <see cref="TeamRoleRemoved" /> events as <see cref="RoleTeamDirectorySchema" />, materialized
///     keyed by team first, so a team's page can list its roles in one read instead of one request
///     per role.
/// </summary>
static class TeamRoleDirectorySchema
{
    public static readonly KvDirectoryIndex<TeamRoleView> ByTeam = new(
        "by_team", 1, static view => [view.TeamId.ToString(), view.RoleId.ToString()]);

    public static readonly KvDirectory<TeamRoleView, (Uuid TeamId, Uuid RoleId)> Directory = new(
        "team-roles",
        ComplianceCoreJsonContext.Default.TeamRoleView,
        static view => (view.TeamId, view.RoleId),
        static identity => [identity.TeamId.ToString(), identity.RoleId.ToString()],
        [ByTeam]);
}

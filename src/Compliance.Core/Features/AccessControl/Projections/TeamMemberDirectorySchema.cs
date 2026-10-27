using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>The shared team-member-directory schema, used by both the read and write sides.</summary>
static class TeamMemberDirectorySchema
{
    public static readonly KvDirectoryIndex<TeamMemberView> ByTeam = new(
        "by_team", 1, static view => [view.TeamId.ToString(), view.MemberId.ToString()]);
    public static readonly KvDirectoryIndex<TeamMemberView> ByMember = new(
        "by_member", 1, static view => [view.MemberId.ToString(), view.TeamId.ToString()]);

    public static readonly KvDirectory<TeamMemberView, (Uuid TeamId, Uuid MemberId)> Directory = new(
        "team-members",
        ComplianceCoreJsonContext.Default.TeamMemberView,
        static view => (view.TeamId, view.MemberId),
        static identity => [identity.TeamId.ToString(), identity.MemberId.ToString()],
        [ByTeam, ByMember]);
}

using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>The shared team-directory schema, used by both the read and write sides.</summary>
static class TeamDirectorySchema
{
    public static readonly KvDirectoryIndex<TeamView> ByName = new(
        "by_name", 1, static team => [Normalize(team.Name)]);

    public static readonly KvDirectory<TeamView, Uuid> Directory = new(
        "teams",
        ComplianceCoreJsonContext.Default.TeamView,
        static team => team.TeamId,
        static teamId => [teamId.ToString()],
        [ByName]);

    public static string Normalize(string value) => value.ToUpperInvariant();
}

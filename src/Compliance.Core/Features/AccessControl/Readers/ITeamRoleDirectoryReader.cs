using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Readers;

/// <summary>Reads the materialized team-role directory for one team.</summary>
public interface ITeamRoleDirectoryReader
{
    /// <summary>Lists the roles granted to one team.</summary>
    /// <param name="search">A role-ID substring filter, or <see langword="null"/> for none.</param>
    ValueTask<Page<TeamRoleView>> ListAsync(
        Uuid tenantId,
        Uuid teamId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default);
}

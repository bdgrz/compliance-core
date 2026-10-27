using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Readers;

/// <summary>Reads the materialized team-member directory for one team.</summary>
public interface ITeamMemberDirectoryReader
{
    /// <summary>Lists one team's members by member ID.</summary>
    /// <param name="search">A member-ID prefix filter, or <see langword="null"/> for none.</param>
    ValueTask<Page<TeamMemberView>> ListAsync(
        Uuid tenantId,
        Uuid teamId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default);

    /// <summary>Checks event history after this directory's checkpoint for a removal not yet projected.</summary>
    ValueTask<bool> HasPendingRemovalAsync(Uuid tenantId, Uuid teamId, Uuid memberId,
        CancellationToken ct = default) => ValueTask.FromResult(false);

    /// <summary>Returns every current team assignment for one member, including unprojected events.</summary>
    ValueTask<IReadOnlyList<TeamMemberView>> ListMemberAssignmentsAsync(Uuid tenantId,
        Uuid memberId, CancellationToken ct = default) =>
        ValueTask.FromResult<IReadOnlyList<TeamMemberView>>([]);
}

using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Projections;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Snapshots.Models;
namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>
///     The program's canonical source records, with their histories, read once for one readiness
///     run. The rules resolve each record as of the assessment time; readiness never stores a
///     second copy of source state. A family whose scan hit the read limit is listed as truncated.
/// </summary>
public sealed record ReadinessSourceSet(IReadOnlyList<ReadinessBoundaryInput> Boundaries,
    IReadOnlyList<ReadinessCommitmentInput> Commitments, IReadOnlyList<ReadinessRiskInput> Risks,
    IReadOnlyList<SnapshotView> Snapshots)
{
    public static ReadinessSourceSet Empty { get; } = new([], [], [], []);

    public IReadOnlyList<ReadinessAccessReviewScopeInput> AccessReviewScopes { get; init; } = [];

    public IReadOnlyList<ReadinessProviderInput> Providers { get; init; } = [];

    public IReadOnlyList<ReadinessTechnologyInventoryInput> TechnologyInventory { get; init; } = [];

    public ReadinessWorkforceRosterSnapshotInput? WorkforceRosterSnapshot { get; init; }

    public IReadOnlyList<string> TruncatedFamilies { get; init; } = [];
}

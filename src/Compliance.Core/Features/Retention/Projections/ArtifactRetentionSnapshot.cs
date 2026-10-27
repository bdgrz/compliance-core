using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Retention.Projections;

/// <summary>A read composition of independent owners, never an admission or disposition authority.</summary>
public sealed record ArtifactRetentionSnapshot(ArtifactRetentionSource Source, ArtifactRetentionBasisRecorded? Basis,
    IReadOnlyList<ArtifactLegalHoldView> Holds, long Revision, string Stamp)
{
    public ArtifactRetentionView Assess(DateOnly assessedAt)
    {
        var active = Holds.Count(static hold => hold.ReleasedAt is null);
        var elapsed = Basis is not null && assessedAt > Basis.RetainsThrough;
        var blockers = new List<string>();
        if (Basis is null)
        {
            blockers.Add("retention_basis_unrecorded");
            blockers.Add("supported_period_unrecorded");
        }
        else if (!elapsed)
            blockers.Add("retention_unelapsed");
        if (active > 0)
            blockers.Add("active_legal_hold");
        blockers.Add("engagement_hold_authority_unavailable");
        blockers.Add("historical_reliance_unavailable");
        blockers.Add("disposition_approval_unavailable");
        blockers.Add(Source.SourceKind == "application_import" ? "inline_disposition_unsupported" : "storage_hold_enforcement_pending");
        return new(Source, Revision, Basis?.Policy, Basis?.PeriodStart, Basis?.PeriodEnd, Basis?.RetainsThrough,
            elapsed, active, Holds.Count, false, blockers.AsReadOnly());
    }
}

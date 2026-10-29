using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Retention.Projections;

/// <summary>Current retention, hold, reliance, and storage facts required for disposition.</summary>
public sealed record ArtifactDispositionEligibilitySnapshot(
    ArtifactRetentionSource Source,
    ulong Position,
    long ContentLength,
    string ArtifactState,
    string? StateReason,
    long HandlingPolicyRevision,
    string HandlingClass,
    bool RetentionBasisRecorded,
    string RetentionPolicy,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly RetainsThrough,
    DateOnly AsOf,
    IReadOnlyList<Uuid> ActiveHoldIds,
    IReadOnlyList<Uuid> HistoricalRelianceIds,
    bool StorageDispositionReady);

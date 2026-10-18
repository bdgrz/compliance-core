using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

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

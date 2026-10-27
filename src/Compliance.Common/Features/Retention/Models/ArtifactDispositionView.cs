using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Approval, execution attempts, and retained tombstone for one artifact disposition.</summary>
public sealed record ArtifactDispositionView(
    Uuid TenantId,
    Uuid DispositionId,
    Uuid ArtifactId,
    string Status,
    long Revision,
    ArtifactRetentionSource Source,
    ulong SourcePosition,
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
    DateOnly ApprovedAsOf,
    IReadOnlyList<Uuid> ActiveHoldIds,
    IReadOnlyList<Uuid> HistoricalRelianceIds,
    string Rationale,
    ActorReference ApprovedBy,
    DateTimeOffset ApprovedAt,
    IReadOnlyList<ArtifactDispositionAttemptView> Attempts,
    ArtifactDispositionTombstoneView? Tombstone,
    DateTimeOffset UpdatedAt);

using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>An immutable Type II period close snapshot and its amendment lineage.</summary>
public sealed record TypeIIPeriodCloseSnapshotView(
    Uuid CloseSnapshotId,
    int Version,
    IReadOnlyList<ExaminationContentReference> SourceReferences,
    IReadOnlyList<Uuid> ExpectedOccurrenceIds,
    IReadOnlyList<Uuid> AcknowledgedMaterialItemIds,
    string ContentSha256,
    Uuid? AmendsCloseSnapshotId,
    string? AmendmentReason,
    IReadOnlyList<Uuid> ChangedRecordIds,
    IReadOnlyList<Uuid> AffectedExaminationMaterialIds,
    Uuid ActorMemberId,
    string ActorRole,
    string ActorDisplay,
    DateTimeOffset RecordedAt)
{
    /// <summary>The authored period revision accepted by an original close freeze; absent on historical unbound snapshots.</summary>
    public long? OriginalExpectedRevision { get; init; }
}

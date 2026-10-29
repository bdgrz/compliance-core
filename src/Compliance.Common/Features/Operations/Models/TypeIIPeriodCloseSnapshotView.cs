using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableContentRef? SourceContentManifest { get; init; }

    /// <summary>The authored period revision accepted by an original close freeze; absent on historical unbound snapshots.</summary>
    public long? OriginalExpectedRevision { get; init; }
}

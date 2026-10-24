using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Close snapshot identity, exact source versions, reconciled population, and acknowledgements.</summary>
public sealed record TypeIIPeriodCloseSnapshotContent(
    Uuid CloseSnapshotId,
    IReadOnlyList<ExaminationContentReference> SourceReferences,
    IReadOnlyList<Uuid> ExpectedOccurrenceIds,
    IReadOnlyList<Uuid> AcknowledgedMaterialItemIds)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableContentRef? SourceContentManifest { get; init; }
}

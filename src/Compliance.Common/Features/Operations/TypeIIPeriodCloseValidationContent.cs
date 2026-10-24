using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Reconciliation facts supplied by close validation over exact source versions.</summary>
public sealed record TypeIIPeriodCloseValidationContent(
    IReadOnlyList<ExaminationContentReference> SourceReferences,
    IReadOnlyList<Uuid> ExpectedOccurrenceIds,
    IReadOnlyList<Uuid> ActualOccurrenceIds,
    IReadOnlyList<Uuid> DuplicateOccurrenceIds,
    IReadOnlyList<Uuid> MissingWorkIds,
    IReadOnlyList<Uuid> UnresolvedMaterialItemIds)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableContentRef? SourceContentManifest { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? RecordedThrough { get; init; }

    /// <summary>Complete source-family capture failures that prohibit a ready close.</summary>
    public IReadOnlyList<string> SourceBlockers { get; init; } = [];
    /// <summary>Retained source scope differences that do not themselves assert a blocking condition.</summary>
    public IReadOnlyList<string> SourceDifferences { get; init; } = [];
}

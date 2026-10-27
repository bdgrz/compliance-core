using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Immutable period-close reconciliation and validator attribution.</summary>
public sealed record TypeIIPeriodCloseValidationView(
    string Status,
    IReadOnlyList<ExaminationContentReference> SourceReferences,
    IReadOnlyList<Uuid> ExpectedOccurrenceIds,
    IReadOnlyList<Uuid> ActualOccurrenceIds,
    IReadOnlyList<Uuid> DuplicateOccurrenceIds,
    IReadOnlyList<Uuid> MissingWorkIds,
    IReadOnlyList<Uuid> UnresolvedMaterialItemIds,
    Uuid ValidatorMemberId,
    string ValidatorDisplay,
    DateTimeOffset ValidatedAt)
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

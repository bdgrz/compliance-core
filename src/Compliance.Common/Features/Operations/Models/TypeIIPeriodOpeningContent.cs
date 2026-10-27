using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>Immutable opening evidence and exact plan-gap acknowledgements for one Type II period.</summary>
public sealed record TypeIIPeriodOpeningContent(
    Uuid ExaminationId,
    Uuid OpeningSnapshotId,
    string OpeningSnapshotSha256,
    IReadOnlyList<ExaminationContentReference> SourceReferences,
    IReadOnlyList<Uuid> AcknowledgedOpeningGapIds)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableContentRef? SourceContentManifest { get; init; }
}

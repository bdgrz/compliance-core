using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Immutable opening evidence and exact plan-gap acknowledgements for one Type II period.</summary>
public sealed record TypeIIPeriodOpeningContent(
    Uuid ExaminationId,
    Uuid OpeningSnapshotId,
    string OpeningSnapshotSha256,
    IReadOnlyList<ExaminationContentReference> SourceReferences,
    IReadOnlyList<Uuid> AcknowledgedOpeningGapIds);

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>An immutable selection bound to the digest of one frozen population.</summary>
public sealed record AuditorSampleSelectionView(
    Uuid SampleSelectionId,
    Uuid PopulationSnapshotId,
    string PopulationContentSha256,
    string SelectionMethod,
    IReadOnlyList<Uuid> SelectedRowIds,
    string SelectionContentSha256,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset RecordedAt);

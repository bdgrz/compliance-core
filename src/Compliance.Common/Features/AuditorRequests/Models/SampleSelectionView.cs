using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

/// <summary>Frozen selection method, exact population reference, selected rows, and manifest.</summary>
public sealed record SampleSelectionView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SampleSelectionId,
    Uuid AuditorRequestId,
    Uuid PopulationSnapshotId,
    int PopulationVersion,
    string PopulationContentSha256,
    string SelectionMethod,
    IReadOnlyList<Uuid> SelectedRowIds,
    string SelectionContentSha256,
    string Status,
    DateTimeOffset UpdatedAt,
    string Source,
    string ExternalReference,
    Uuid? SupersedesSelectionId,
    string? AmendmentReason);

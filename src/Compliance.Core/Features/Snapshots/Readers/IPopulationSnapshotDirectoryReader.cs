using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Snapshots.Models;
namespace Bdgrz.Compliance.Features.Snapshots.Readers;

public interface IPopulationSnapshotDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);

    /// <summary>Lists one kind's snapshots, newest frozen first.</summary>
    ValueTask<Page<PopulationSnapshotSummary>> ListAsync(Uuid tenantId, string kind, int limit,
        string? cursor, CancellationToken ct = default);
}

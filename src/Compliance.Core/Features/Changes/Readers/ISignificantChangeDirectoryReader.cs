using Bdgrz.Compliance.Features.Changes.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes.Readers;

public interface ISignificantChangeDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default);

    ValueTask<SignificantChangeView?> GetAsync(Uuid tenantId, Uuid changeId,
        CancellationToken ct = default);

    ValueTask<Page<SignificantChangeSummaryView>> ListAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
}

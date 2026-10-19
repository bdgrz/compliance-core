using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

public interface ISignificantChangeDirectoryReader
{
    ValueTask<SignificantChangeView?> GetAsync(Uuid tenantId, Uuid changeId,
        CancellationToken ct = default);

    ValueTask<Page<SignificantChangeSummaryView>> ListAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
}

using Bdgrz.Compliance.Features.Providers.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Readers;

public interface IAssuranceReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default);

    ValueTask<AssuranceReportView?> GetReportRevisionAsync(Uuid tenantId, Uuid providerId, Uuid reportId,
        long revision, CancellationToken ct = default);

    ValueTask<Page<AssuranceReportView>> ListReportsAsync(Uuid tenantId, Uuid providerId, int limit, string? cursor,
        CancellationToken ct = default);

    ValueTask<ProviderReviewView?> GetReviewAsync(Uuid tenantId, Uuid reviewId, CancellationToken ct = default);

    ValueTask<Page<ProviderReviewView>> ListReviewsAsync(Uuid tenantId, Uuid providerId, int limit, string? cursor,
        CancellationToken ct = default);

    ValueTask<ProviderCoverageGapView?> GetCoverageGapAsync(Uuid tenantId, Uuid providerId, Uuid gapId,
        CancellationToken ct = default);

    ValueTask<Page<ProviderCoverageGapView>> ListCoverageGapsAsync(Uuid tenantId, Uuid providerId,
        int limit, string? cursor, CancellationToken ct = default);
}

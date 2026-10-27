using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Readers;

public interface IRiskDraftHistoryDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid riskId,
        long revision, CancellationToken ct = default);
    ValueTask<Page<RiskDraftRevisionView>> ListRevisionsAsync(Uuid tenantId, Uuid riskId,
        int limit, string? cursor, CancellationToken ct = default);
}

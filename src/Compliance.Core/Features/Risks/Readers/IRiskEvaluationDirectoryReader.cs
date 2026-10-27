using Cntryl.Portia;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Readers;

public interface IRiskEvaluationDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);

    ValueTask<RiskEvaluationView?> GetAsync(Uuid tenantId, Uuid riskId,
        CancellationToken ct = default);
    ValueTask<Page<RiskEvaluationHistoryEntryView>> ListHistoryAsync(Uuid tenantId, Uuid riskId,
        int limit, string? cursor, CancellationToken ct = default);
}

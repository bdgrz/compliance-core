using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Controls.Readers;

public interface IControlDraftHistoryDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<ControlDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid controlId,
        long revision, CancellationToken ct = default);
    ValueTask<Page<ControlDraftRevisionView>> ListRevisionsAsync(Uuid tenantId,
        Uuid controlId, int limit, string? cursor, CancellationToken ct = default);
}

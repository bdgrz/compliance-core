using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Commitments.Readers;

public interface ICommitmentDraftHistoryDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<CommitmentDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid draftId,
        long revision, CancellationToken ct = default);
    ValueTask<Page<CommitmentDraftRevisionView>> ListRevisionsAsync(Uuid tenantId,
        Uuid draftId, int limit, string? cursor, CancellationToken ct = default);
}

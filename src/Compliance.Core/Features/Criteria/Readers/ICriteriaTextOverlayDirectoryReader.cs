using Cntryl.Portia;
using Bdgrz.Compliance.Features.Criteria.Models;
namespace Bdgrz.Compliance.Features.Criteria.Readers;

interface ICriteriaTextOverlayDirectoryReader
{
    ValueTask<Result<IReadOnlyDictionary<string, CriteriaTextOverlayRevision>>> GetManyAsync(
        Uuid tenantId, Uuid editionId, IReadOnlyList<string> identifiers,
        CancellationToken ct = default);

    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
}

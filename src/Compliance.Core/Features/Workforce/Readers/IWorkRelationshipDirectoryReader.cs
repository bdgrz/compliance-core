using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Readers;

public interface IWorkRelationshipDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<WorkRelationshipView?> GetAsync(Uuid tenantId, Uuid relationshipId,
        CancellationToken ct = default);
    ValueTask<Page<WorkRelationshipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default);
}

using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Readers;

public interface IPersonDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<PersonView?> GetAsync(Uuid tenantId, Uuid personId,
        CancellationToken ct = default);
    ValueTask<Page<PersonView>> ListAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default);
}

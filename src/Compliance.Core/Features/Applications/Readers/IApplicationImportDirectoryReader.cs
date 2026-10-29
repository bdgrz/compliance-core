using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Readers;

public interface IApplicationImportDirectoryReader
{
    ValueTask<ApplicationImportView?> GetAsync(Uuid tenantId, Uuid batchId,
        CancellationToken ct = default);
    ValueTask<Page<ApplicationImportRowView>> ListRowsAsync(Uuid tenantId, Uuid batchId,
        int limit, string? cursor, CancellationToken ct = default);
}

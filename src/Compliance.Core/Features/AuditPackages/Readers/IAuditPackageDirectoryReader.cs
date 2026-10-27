using Bdgrz.Compliance.Features.AuditPackages.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Readers;

/// <summary>Discovers immutable package identities by tenant/program and creation time; rows are not current status.</summary>
public interface IAuditPackageDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default);
    ValueTask<Page<AuditPackageSummaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
}

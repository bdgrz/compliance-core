using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public interface IComplianceIncidentDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default);

    ValueTask<ComplianceIncidentView?> GetAsync(Uuid tenantId, Uuid changeId,
        CancellationToken ct = default);

    ValueTask<Page<ComplianceIncidentSummaryView>> ListAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
}

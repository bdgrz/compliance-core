using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public interface IComplianceIncidentDirectoryReader
{
    ValueTask<ComplianceIncidentView?> GetAsync(Uuid tenantId, Uuid incidentId,
        CancellationToken ct = default);

    ValueTask<Page<ComplianceIncidentSummaryView>> ListAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
}

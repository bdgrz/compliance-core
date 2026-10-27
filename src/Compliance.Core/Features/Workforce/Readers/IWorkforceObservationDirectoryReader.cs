using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Readers;

public interface IWorkforceObservationDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<Page<WorkforceObservationView>> ListAsync(Uuid tenantId, string? kind, int limit,
        string? cursor, CancellationToken ct = default);
    ValueTask<WorkforceObservationView?> GetAsync(Uuid tenantId, Uuid observationId,
        CancellationToken ct = default);
    /// <summary>Returns the lifecycle status of every recorded relationship of one person.</summary>
    ValueTask<IReadOnlyList<string>> ListRelationshipStatusesAsync(Uuid tenantId, Uuid personId,
        CancellationToken ct = default);
}

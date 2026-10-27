using Bdgrz.Compliance.Features.Operations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Readers;

/// <summary>Discovers immutable package identities by tenant/program and creation time; rows are not current status.</summary>
public interface IObservationPeriodDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default);
    ValueTask<Page<ObservationPeriodIdentity>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
}

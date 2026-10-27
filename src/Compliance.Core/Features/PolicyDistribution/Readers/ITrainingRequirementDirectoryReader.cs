using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public interface ITrainingRequirementDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default);

    /// <summary>The program's requirements at their latest version, ordered by identifier.</summary>
    ValueTask<Page<TrainingRequirementView>> ListProgramAsync(Uuid tenantId, Uuid programId, int limit,
        string? cursor, CancellationToken ct = default);
}

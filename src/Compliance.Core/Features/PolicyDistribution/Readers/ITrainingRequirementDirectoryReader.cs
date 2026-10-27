using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Readers;

public interface ITrainingRequirementDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default);

    /// <summary>The program's requirements at their latest version, ordered by identifier.</summary>
    ValueTask<Page<TrainingRequirementView>> ListProgramAsync(Uuid tenantId, Uuid programId, int limit,
        string? cursor, CancellationToken ct = default);
}

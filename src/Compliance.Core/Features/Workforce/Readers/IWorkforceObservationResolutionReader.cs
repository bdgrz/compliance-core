using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Readers;

public interface IWorkforceObservationResolutionReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    /// <summary>Returns the closures that exist among the given observations of one tenant.</summary>
    ValueTask<IReadOnlyDictionary<Uuid, WorkforceObservationResolutionView>> GetManyAsync(
        Uuid tenantId, IReadOnlyCollection<Uuid> observationIds, CancellationToken ct = default);
}

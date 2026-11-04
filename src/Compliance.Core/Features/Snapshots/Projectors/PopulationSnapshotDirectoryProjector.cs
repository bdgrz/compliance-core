using Cntryl.Portia;
using Bdgrz.Compliance.Features.Snapshots.Events;
using Bdgrz.Compliance.Features.Snapshots.Projections;
namespace Bdgrz.Compliance.Features.Snapshots.Projectors;

public sealed partial class PopulationSnapshotDirectoryProjector(
    IPopulationSnapshotDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("population-snapshots"),
            "PopulationSnapshotDirectoryV2"),
      IProjectorHandler<PopulationSnapshotFrozen>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<PopulationSnapshotFrozen> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}

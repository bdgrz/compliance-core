using Cntryl.Portia;
using Bdgrz.Compliance.Features.Snapshots.Events;
using Bdgrz.Compliance.Features.Snapshots.Projections;
namespace Bdgrz.Compliance.Features.Snapshots.Projectors;

public sealed partial class SnapshotDirectoryProjector(ISnapshotDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("snapshots"), "SnapshotDirectory"),
      IProjectorHandler<SnapshotFrozen>
{
    public ValueTask HandleAsync(SnapshotFrozen ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}

using Cntryl.Portia;
using Bdgrz.Compliance.Features.TechnologyInventory.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Projections;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Projectors;

public sealed partial class TechnologyInventoryDirectoryProjector(
    ITechnologyInventoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(TechnologyInventoryStreams.Area),
            FitzTechnologyInventoryDirectory.ProjectorName),
        IProjectorHandler<TechnologyComponentRevisionRecorded>,
        IProjectorHandler<InformationAssetRevisionRecorded>,
        IProjectorHandler<DataFlowRevisionRecorded>
{
    public ValueTask HandleAsync(TechnologyComponentRevisionRecorded ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(InformationAssetRevisionRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(DataFlowRevisionRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}

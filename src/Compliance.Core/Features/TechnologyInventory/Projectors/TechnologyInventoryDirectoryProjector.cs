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
    public async ValueTask<Result> HandleAsync(IProjectorContext<TechnologyComponentRevisionRecorded> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<InformationAssetRevisionRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<DataFlowRevisionRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}

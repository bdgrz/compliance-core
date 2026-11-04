using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Projections;
namespace Bdgrz.Compliance.Features.AccessReviews.Projectors;

public sealed partial class AccessPopulationDirectoryProjector(
    IAccessPopulationDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AccessPopulation.Area),
            AccessReviewDirectorySchema.PopulationProjector),
        IProjectorHandler<AccessPopulationOpened>,
        IProjectorHandler<AccessPopulationFactsRecorded>,
        IProjectorHandler<AccessPopulationAccepted>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<AccessPopulationOpened> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<AccessPopulationFactsRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<AccessPopulationAccepted> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}

using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Boundaries.Events;
using Bdgrz.Compliance.Features.Boundaries.Projections;
namespace Bdgrz.Compliance.Features.Boundaries.Projectors;

public sealed partial class BoundaryDirectoryProjector(IBoundaryDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), CassieBoundaryDirectory.ProjectorName),
      IProjectorHandler<BoundaryDraftCreated>, IProjectorHandler<BoundaryDraftRevised>,
      IProjectorHandler<BoundaryDraftDiscarded>,
      IProjectorHandler<BoundaryReviewed>, IProjectorHandler<BoundaryApproved>,
      IProjectorHandler<BoundarySuccessorProposed>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<BoundaryDraftCreated> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<BoundaryDraftRevised> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<BoundaryDraftDiscarded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<BoundaryReviewed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<BoundaryApproved> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<BoundarySuccessorProposed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}

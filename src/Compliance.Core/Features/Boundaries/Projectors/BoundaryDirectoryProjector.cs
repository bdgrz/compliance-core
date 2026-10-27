using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Boundaries.Events;
using Bdgrz.Compliance.Features.Boundaries.Projections;
namespace Bdgrz.Compliance.Features.Boundaries.Projectors;

public sealed partial class BoundaryDirectoryProjector(IBoundaryDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), FitzBoundaryDirectory.ProjectorName),
      IProjectorHandler<BoundaryDraftCreated>, IProjectorHandler<BoundaryDraftRevised>,
      IProjectorHandler<BoundaryDraftDiscarded>,
      IProjectorHandler<BoundaryReviewed>, IProjectorHandler<BoundaryApproved>,
      IProjectorHandler<BoundarySuccessorProposed>
{
    public ValueTask HandleAsync(BoundaryDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(BoundaryDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(BoundaryDraftDiscarded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(BoundaryReviewed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(BoundaryApproved ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(BoundarySuccessorProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}

using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Applications.Projections;
using Bdgrz.Compliance.Features.Boundaries.Events;
namespace Bdgrz.Compliance.Features.Applications.Projectors;

/// <summary>Replays boundary events into a separately versioned, tenant-scoped reverse index.</summary>
public sealed partial class ApplicationBoundaryReferenceProjector(
    IApplicationBoundaryReferenceProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("boundaries"),
        "ApplicationBoundaryReferencesV3"),
      IProjectorHandler<BoundaryDraftCreated>, IProjectorHandler<BoundaryDraftRevised>,
      IProjectorHandler<BoundaryDraftDiscarded>, IProjectorHandler<BoundaryReviewed>,
      IProjectorHandler<BoundaryApproved>, IProjectorHandler<BoundarySuccessorProposed>
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

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

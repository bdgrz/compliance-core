using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Work.Projections;
namespace Bdgrz.Compliance.Features.Work.Projectors;

public sealed partial class ControlOccurrenceWorkItemProjector(
    IControlOccurrenceWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(),
        FitzControlOccurrenceWorkItemDirectory.ProjectorName),
      IProjectorHandler<ControlOperatingPlanProposed>,
      IProjectorHandler<ControlOperatingPlanApproved>,
      IProjectorHandler<ControlOccurrenceOpened>,
      IProjectorHandler<ControlOccurrenceAttested>,
      IProjectorHandler<ControlOccurrenceReviewed>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlOperatingPlanProposed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlOperatingPlanApproved> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlOccurrenceOpened> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlOccurrenceAttested> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlOccurrenceReviewed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}

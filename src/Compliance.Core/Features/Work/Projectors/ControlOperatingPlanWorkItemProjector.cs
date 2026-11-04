using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Work.Projections;
namespace Bdgrz.Compliance.Features.Work.Projectors;

public sealed partial class ControlOperatingPlanWorkItemProjector(
    IControlOperatingPlanWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(ControlOperatingPlan.Area),
        FitzControlOperatingPlanWorkItemDirectory.ProjectorName),
      IProjectorHandler<ControlOperatingPlanProposed>,
      IProjectorHandler<ControlOperatingPlanApproved>
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
}

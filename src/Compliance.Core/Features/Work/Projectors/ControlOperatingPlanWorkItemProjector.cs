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
    public ValueTask HandleAsync(ControlOperatingPlanProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlOperatingPlanApproved ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}

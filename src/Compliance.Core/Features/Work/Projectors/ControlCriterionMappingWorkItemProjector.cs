using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.ControlMappings.Events;
using Bdgrz.Compliance.Features.Work.Projections;
namespace Bdgrz.Compliance.Features.Work.Projectors;

public sealed partial class ControlCriterionMappingWorkItemProjector(
    IControlCriterionMappingWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("control-criterion-mappings"),
        FitzControlMappingWorkItemDirectory.ProjectorName),
      IProjectorHandler<ControlCriterionMappingProposed>,
      IProjectorHandler<ControlCriterionMappingReviewed>,
      IProjectorHandler<ControlCriterionMappingRetired>
{
    public ValueTask HandleAsync(ControlCriterionMappingProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlCriterionMappingReviewed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlCriterionMappingRetired ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}

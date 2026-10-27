using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.ControlMappings.Events;
using Bdgrz.Compliance.Features.Work.Projections;
namespace Bdgrz.Compliance.Features.Work.Projectors;

public sealed partial class CriterionApplicabilityWorkItemProjector(
    ICriterionApplicabilityWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("criterion-applicability"),
        FitzCriterionApplicabilityWorkItemDirectory.ProjectorName),
      IProjectorHandler<CriterionNotApplicableProposed>,
      IProjectorHandler<CriterionApplicabilityReviewed>,
      IProjectorHandler<CriterionNotApplicableWithdrawn>
{
    public ValueTask HandleAsync(CriterionNotApplicableProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CriterionApplicabilityReviewed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CriterionNotApplicableWithdrawn ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}

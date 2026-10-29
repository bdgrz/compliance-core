using Cntryl.Portia;
using Bdgrz.Compliance.Features.ControlMappings.Events;
using Bdgrz.Compliance.Features.ControlMappings.Projections;
namespace Bdgrz.Compliance.Features.ControlMappings.Projectors;

public sealed partial class CriterionApplicabilityDirectoryV1Projector(
    ICriterionApplicabilityDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("criterion-applicability"),
            "CriterionApplicabilityDirectoryV1"),
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

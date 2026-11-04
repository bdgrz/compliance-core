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
    public async ValueTask<Result> HandleAsync(IProjectorContext<CriterionNotApplicableProposed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<CriterionApplicabilityReviewed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<CriterionNotApplicableWithdrawn> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}

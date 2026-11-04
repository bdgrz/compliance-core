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
    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlCriterionMappingProposed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlCriterionMappingReviewed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlCriterionMappingRetired> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}

using Cntryl.Portia;
using Bdgrz.Compliance.Features.ControlMappings.Events;
using Bdgrz.Compliance.Features.ControlMappings.Projections;
namespace Bdgrz.Compliance.Features.ControlMappings.Projectors;

public sealed partial class ControlMappingDirectoryV1Projector(
    IControlMappingDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("control-criterion-mappings"),
            "ControlMappingDirectoryV1"),
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

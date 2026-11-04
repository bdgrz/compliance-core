using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Projections;
namespace Bdgrz.Compliance.Features.Workforce.Projectors;

public sealed partial class WorkforceObservationResolutionProjector(
    IWorkforceObservationResolutionProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("workforce-observation-resolutions"),
            "WorkforceObservationResolutionsV1"),
      IProjectorHandler<WorkforceObservationResolved>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<WorkforceObservationResolved> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}

using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Projections;
namespace Bdgrz.Compliance.Features.Workforce.Projectors;

public sealed partial class WorkforceObservationProjector(IWorkforceObservationProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("work-relationships"),
            "WorkforceObservationsV1"),
      IProjectorHandler<WorkRelationshipRecorded>, IProjectorHandler<WorkRelationshipRevised>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<WorkRelationshipRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<WorkRelationshipRevised> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}

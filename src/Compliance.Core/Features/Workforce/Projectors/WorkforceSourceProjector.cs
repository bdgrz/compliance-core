using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Projections;
namespace Bdgrz.Compliance.Features.Workforce.Projectors;

public sealed partial class WorkforceSourceProjector(IWorkforceSourceProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("workforce-source-observations"), "WorkforceSourcesV1"),
      IProjectorHandler<WorkforceSourceObserved>, IProjectorHandler<WorkforceSourceReconciled>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<WorkforceSourceObserved> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<WorkforceSourceReconciled> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}

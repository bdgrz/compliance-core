using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.Work.Projections;
namespace Bdgrz.Compliance.Features.Work.Projectors;

public sealed partial class ControlEvaluationWorkItemProjector(
    IControlEvaluationWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("control-evaluations"),
        FitzControlEvaluationWorkItemDirectory.ProjectorName),
      IProjectorHandler<ControlEvaluationStarted>,
      IProjectorHandler<ControlEvaluationStepRecorded>,
      IProjectorHandler<ControlEvaluationDeviationDisposed>,
      IProjectorHandler<ControlEvaluationSubmitted>,
      IProjectorHandler<ControlEvaluationReviewed>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlEvaluationStarted> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlEvaluationStepRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlEvaluationDeviationDisposed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlEvaluationSubmitted> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlEvaluationReviewed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}

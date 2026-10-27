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
    public ValueTask HandleAsync(ControlEvaluationStarted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlEvaluationStepRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlEvaluationDeviationDisposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlEvaluationSubmitted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlEvaluationReviewed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}

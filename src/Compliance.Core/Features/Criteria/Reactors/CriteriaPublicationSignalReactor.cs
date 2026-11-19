using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Reactors;

/// <summary>Persists each publication participant signal without waiting for its outcome.</summary>
public sealed partial class CriteriaPublicationSignalReactor(IProjectionCheckpointStore checkpoints, CriteriaPublicationSignalIntake intake)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz"), "CriteriaPublicationSignalV1"),
      IReactorHandler<CriteriaPublicationStarted>, IReactorHandler<CriterionWitnessPinned>,
      IReactorHandler<CriteriaPublicationAborted>, IReactorHandler<CriterionWitnessPinFenced>,
      IReactorHandler<CriterionWitnessRefused>
{
    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaPublicationStarted> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);
    public ValueTask<Result> HandleAsync(IReactorContext<CriterionWitnessPinned> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);
    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaPublicationAborted> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);
    public ValueTask<Result> HandleAsync(IReactorContext<CriterionWitnessPinFenced> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);
    public ValueTask<Result> HandleAsync(IReactorContext<CriterionWitnessRefused> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);
}

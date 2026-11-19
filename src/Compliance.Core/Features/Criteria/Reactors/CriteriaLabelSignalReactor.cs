using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Reactors;

/// <summary>Persists independent signals without driving contested claims on the shared checkpoint.</summary>
public sealed partial class CriteriaLabelSignalReactor(IProjectionCheckpointStore checkpoints, CriteriaLabelSignalIntake intake)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz"), "CriteriaLabelSignalV1"),
      IReactorHandler<CriteriaEditionDraftRequested>,
      IReactorHandler<CriteriaInitialLabelRequested>,
      IReactorHandler<CriteriaEditionRenameRequested>,
      IReactorHandler<CriteriaEditionDraftActivated>,
      IReactorHandler<CriteriaEditionDraftRenamed>,
      IReactorHandler<CriteriaEditionDraftRequestCancelled>,
      IReactorHandler<CriteriaEditionRenameCancelled>,
      IReactorHandler<CriteriaInitialLabelRefused>,
      IReactorHandler<CriteriaEditionRenameRefused>,
      IReactorHandler<CriteriaEditionDraftAbandoned>,
      IReactorHandler<CriteriaEditionRenameCompleted>,
      IReactorHandler<EditionLabelReserved>,
      IReactorHandler<EditionLabelConfirmed>, IReactorHandler<EditionLabelReservationRefused>
{
    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaEditionDraftRequested> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaInitialLabelRequested> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaEditionRenameRequested> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaEditionDraftActivated> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaEditionDraftRenamed> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaEditionDraftRequestCancelled> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaEditionRenameCancelled> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaInitialLabelRefused> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaEditionRenameRefused> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaEditionDraftAbandoned> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaEditionRenameCompleted> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<EditionLabelReserved> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<EditionLabelConfirmed> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<EditionLabelReservationRefused> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);
}

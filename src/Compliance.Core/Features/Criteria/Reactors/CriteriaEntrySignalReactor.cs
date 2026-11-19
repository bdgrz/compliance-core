using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Reactors;

/// <summary>Durably schedules proposal identity; decisions retry independently.</summary>
public sealed partial class CriteriaEntrySignalReactor(IProjectionCheckpointStore checkpoints, CriteriaEntrySignalIntake intake)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz"), "CriteriaEntrySignalV1"),
      IReactorHandler<CriterionChangeProposed>, IReactorHandler<CriterionSeedProposed>
{
    public ValueTask<Result> HandleAsync(IReactorContext<CriterionChangeProposed> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<CriterionSeedProposed> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);
}

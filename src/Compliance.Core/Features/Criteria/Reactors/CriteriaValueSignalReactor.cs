using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Reactors;

/// <summary>Durably schedules each value proposal without waiting for its outcome.</summary>
public sealed partial class CriteriaValueSignalReactor(IProjectionCheckpointStore checkpoints, CriteriaValueSignalIntake intake)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz"), "CriteriaValueSignalV1"),
      IReactorHandler<CriteriaEditionValueProposed>
{
    public ValueTask<Result> HandleAsync(IReactorContext<CriteriaEditionValueProposed> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);
}

using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

/// <summary>Schedules each primary seat signal independently before checkpointing it.</summary>
public sealed partial class PlatformPrimarySignalReactor(IProjectionCheckpointStore checkpoints, PlatformPrimarySignalIntake intake)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz"), "PlatformPrimarySignalV1"),
      IReactorHandler<PrimaryPlatformOperatorSeatReserved>, IReactorHandler<PrimaryPlatformOperatorSeatReleased>
{
    public ValueTask<Result> HandleAsync(IReactorContext<PrimaryPlatformOperatorSeatReserved> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);
    public ValueTask<Result> HandleAsync(IReactorContext<PrimaryPlatformOperatorSeatReleased> context, CancellationToken ct) =>
        intake.AcceptAsync(context.Source.Stream, context.Source.ResourceOffset, context.Trigger, ct);
}

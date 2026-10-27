using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Reactors;

/// <summary>
///     Starts <c>TypeIIRangeProcess</c> from a committed opening or amendment acceptance. The process record exists
///     before the source checkpoint advances; the sweep resumes it if this reactor stops. The intent is already
///     persisted on the period, so the calendar is touched only after it.
/// </summary>
sealed partial class TypeIIRangeReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(TypeIIPeriod.Area), WorkloadName),
        IReactorHandler<TypeIIPeriodOpeningAccepted>, IReactorHandler<TypeIIPeriodAmendmentAccepted>
{
    public const string WorkloadName = "TypeIIRangeV1";

    public async ValueTask HandleAsync(IReactorContext<TypeIIPeriodOpeningAccepted> context, CancellationToken ct) =>
        _ = await process.StartAsync(TypeIIRangeEffects.ProcessName, context, context.Trigger.Intent, [],
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);

    public async ValueTask HandleAsync(IReactorContext<TypeIIPeriodAmendmentAccepted> context, CancellationToken ct) =>
        _ = await process.StartAsync(TypeIIRangeEffects.ProcessName, context, context.Trigger.Intent, [],
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
}

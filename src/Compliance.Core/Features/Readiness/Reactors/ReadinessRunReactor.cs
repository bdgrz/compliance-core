using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Processes;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Reactors;

/// <summary>
///     Starts <c>ReadinessRunProcess</c> from a committed run admission. The process record exists before the source
///     checkpoint advances and the sweep resumes it if this reactor stops, so an admitted run is always computed and
///     adopted exactly once even when the reactor, a receiver or the process worker crashes in between. The run has no
///     per-target stage.
/// </summary>
sealed partial class ReadinessRunReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ProgramReadiness.Area), WorkloadName),
        IReactorHandler<ReadinessAssessmentRequested>
{
    public const string WorkloadName = "ReadinessRunV1";

    public async ValueTask HandleAsync(IReactorContext<ReadinessAssessmentRequested> context, CancellationToken ct) =>
        _ = await process.StartAsync(ReadinessEffects.RunProcessName, context, context.Trigger.Intent, [],
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
}

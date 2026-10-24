using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Starts <c>EngagementPersonalDecisionProcess</c> from a committed personal decision admission. The process
///     record, with its frozen intent, exists before the source checkpoint advances; the sweep resumes it if this
///     reactor stops.
/// </summary>
sealed partial class EngagementPersonalDecisionReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ServiceEngagementIndependence.Area), WorkloadName),
        IReactorHandler<EngagementPersonalDecisionAdmitted>
{
    public const string WorkloadName = "EngagementPersonalDecisionV1";

    public async ValueTask HandleAsync(IReactorContext<EngagementPersonalDecisionAdmitted> context,
        CancellationToken ct) => _ = await process.StartAsync(EngagementPersonalDecisionEffects.ProcessName, context,
        context.Trigger.Intent, [], $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
}

using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Starts <c>IndependentChildMaterialization</c> from a committed corrective action admission. The process record
///     exists before the source checkpoint advances and the sweep resumes it if this reactor stops, so an admission is
///     always materialized exactly once even when the reactor, the receiver or the process worker crashes in between.
/// </summary>
sealed partial class CorrectiveActionMaterializationReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(FindingAggregate.Area), WorkloadName),
        IReactorHandler<CorrectiveActionAdmitted>
{
    public const string WorkloadName = "CorrectiveActionMaterializationV1";

    public async ValueTask HandleAsync(IReactorContext<CorrectiveActionAdmitted> context, CancellationToken ct) =>
        _ = await process.StartAsync(FindingClosureEffects.MaterializationProcessName, context, context.Trigger.Intent,
            [context.Trigger.Operation.LogicalId.ToString()], $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
}

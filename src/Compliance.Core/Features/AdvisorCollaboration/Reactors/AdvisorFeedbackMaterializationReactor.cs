using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Reactors;

/// <summary>
///     Starts <c>IndependentChildMaterialization</c> from a committed advisor feedback admission. The process record
///     exists before the source checkpoint advances and the sweep resumes it if this reactor stops, so an admission is
///     always materialized exactly once even when the reactor, the receiver or the process worker crashes in between,
///     and also when the handoff is revoked or closed after the admission.
/// </summary>
sealed partial class AdvisorFeedbackMaterializationReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AdvisorHandoff.Area), WorkloadName),
        IReactorHandler<AdvisorFeedbackAdmitted>
{
    public const string WorkloadName = "AdvisorFeedbackMaterializationV1";

    public async ValueTask HandleAsync(IReactorContext<AdvisorFeedbackAdmitted> context, CancellationToken ct) =>
        _ = await process.StartAsync(FindingClosureEffects.MaterializationProcessName, context, context.Trigger.Intent,
            [context.Trigger.Operation.LogicalId.ToString()], $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
}

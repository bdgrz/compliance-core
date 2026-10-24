using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Starts <c>IndependentChildMaterialization</c> from a committed participant result admission. The process
///     record, with the single participant target taken from the admission's affected owner, exists before the source
///     checkpoint advances; the sweep resumes it if this reactor stops.
/// </summary>
sealed partial class PolicyParticipantResultReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(PolicyDistributionCampaign.Area), WorkloadName),
        IReactorHandler<PolicyParticipantResultAdmitted>
{
    public const string WorkloadName = "PolicyParticipantResultV1";

    public async ValueTask HandleAsync(IReactorContext<PolicyParticipantResultAdmitted> context, CancellationToken ct) =>
        _ = await process.StartAsync(IndependentChildMaterialization.ProcessName, context, context.Trigger.Intent,
            [context.Trigger.Operation.AffectedOwner.Resource], $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
}

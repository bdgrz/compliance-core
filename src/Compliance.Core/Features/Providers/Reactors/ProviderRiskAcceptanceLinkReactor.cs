using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Reactors;

/// <summary>
///     Starts <c>IndependentChildMaterialization</c> from a committed link admission. The process record, with its
///     frozen intent, exists before the source checkpoint advances; the sweep resumes it if this reactor stops.
/// </summary>
sealed partial class ProviderRiskAcceptanceLinkReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ProviderAssuranceStreams.Area), WorkloadName),
        IReactorHandler<ProviderCoverageGapRiskAcceptanceAdmitted>
{
    public const string WorkloadName = "ProviderRiskAcceptanceLinkV1";

    public async ValueTask HandleAsync(IReactorContext<ProviderCoverageGapRiskAcceptanceAdmitted> context,
        CancellationToken ct) =>
        _ = await process.StartAsync(IndependentChildMaterializationProcess.Name, context, context.Trigger.Intent, [],
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
}

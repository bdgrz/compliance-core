using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Providers.Events;
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

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
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Reactors;

/// <summary>
///     Starts <c>Bdgrz.Compliance.Features.PolicyDistribution.Processes.IndependentChildMaterialization</c> from a committed participant result admission. The process
///     record, with the single participant target taken from the admission's affected owner, exists before the source
///     checkpoint advances; the sweep resumes it if this reactor stops.
/// </summary>
sealed partial class PolicyParticipantResultReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(PolicyDistributionCampaign.Area), WorkloadName),
        IReactorHandler<PolicyParticipantResultAdmitted>
{
    public const string WorkloadName = "PolicyParticipantResultV1";

    public async ValueTask HandleAsync(IReactorContext<PolicyParticipantResultAdmitted> context, CancellationToken ct) =>
        _ = await process.StartAsync(Bdgrz.Compliance.Features.PolicyDistribution.Processes.IndependentChildMaterialization.ProcessName, context, context.Trigger.Intent,
            [context.Trigger.Operation.AffectedOwner.Resource], $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
}

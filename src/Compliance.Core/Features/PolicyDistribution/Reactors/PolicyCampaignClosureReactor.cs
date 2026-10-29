using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Reactors;

/// <summary>
///     Starts <c>PolicyCampaignClosureProcess</c> from a committed closure acceptance. The process record, with the
///     complete canonical participant list of the frozen intent, exists before the source checkpoint advances; the
///     sweep resumes it if this reactor stops. A closure intent that cannot be read yet is transient: no record is
///     created from an unverified closure.
/// </summary>
sealed partial class PolicyCampaignClosureReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(PolicyDistributionCampaign.Area), WorkloadName),
        IReactorHandler<PolicyCampaignClosureAccepted>
{
    public const string WorkloadName = "PolicyCampaignClosureV1";

    public async ValueTask HandleAsync(IReactorContext<PolicyCampaignClosureAccepted> context, CancellationToken ct)
    {
        IReadOnlyList<ClosureParticipant> participants;
        try
        {
            var tenantId = PolicyCampaignContent.ParseTenant(context.Source.Stream.Realm);
            var intent = await PolicyCampaignContent.ReadAsync<ClosureIntent>(content, tenantId,
                context.Trigger.Intent, ct).ConfigureAwait(false);
            participants = await ClosureParticipant.ReadAsync(content, tenantId, intent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The closure intent cannot be verified yet: {exception.Message}", true));
        }

        _ = await process.StartAsync(PolicyCampaignClosureEffects.ProcessName, context, context.Trigger.Intent,
            participants.Select(static participant => participant.Target), $"reactor:{WorkloadName}", ct)
            .ConfigureAwait(false);
    }
}

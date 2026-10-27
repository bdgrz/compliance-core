using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Processes;

/// <summary>
///     Ends a pending closure that can definitely never complete: the closure is canceled first, which fences the
///     delayed finalization against late delivery, and only then is every participant use settled as canceled, even a
///     use that was never acquired, so a delayed acquisition is refused. All effects use the deterministic command ids
///     the process driver would use, so a later driver step replays them. Nothing is inferred from missing state.
/// </summary>
sealed class PolicyCampaignClosureFences(IRequestBus bus)
{
    public async ValueTask<Result<ReceiverOutcome>> AbortAndFenceAsync(PolicyCampaignClosure closure,
        NativeFactReference source, ImmutableContentRef intentReference, string reason, IExecutionContext context,
        CancellationToken ct)
    {
        var (campaign, accepted, intent) = (closure.Campaign!, closure.Accepted!, closure.Intent!);
        var root = intent.Header.RootOperationId;
        var episode = intent.Header.Episode;
        var abortId = PolicyCampaignClosureEffects.ReceiverCommandId(source.EventId,
            nameof(AbortPolicyCampaignClosure), null, episode);
        var aborted = await bus.SendAsync(new AbortPolicyCampaignClosure(abortId, root, campaign.Stream,
            accepted.Generation, accepted.Revision, intentReference), context, ct).ConfigureAwait(false);
        if (!aborted.IsSuccess)
            return aborted;
        var evidence = aborted.Value switch
        {
            ReceiverOutcome.Applied applied => applied.Fact,
            ReceiverOutcome.Replayed replayed => replayed.Fact,
            ReceiverOutcome.Superseded superseded => superseded.Evidence,
            _ => null,
        };
        // A closure that already completed recovers forward: its own settlement releases the uses.
        if (evidence is null)
            return aborted;
        foreach (var participant in closure.Participants!)
        {
            var settlementId = PolicyCampaignClosureEffects.ReceiverCommandId(source.EventId,
                nameof(SettlePolicyCampaignParticipantCampaignClosureUse), participant.Target, episode);
            var settled = await bus.SendAsync(new SettlePolicyCampaignParticipantCampaignClosureUse(settlementId,
                evidence, participant.Requirement.SourceOwner, root, accepted.Generation, null, intentReference,
                Uuid.Parse(participant.Target, System.Globalization.CultureInfo.InvariantCulture), campaign.Id,
                participant.PersonId, SourceUseSettlementDisposition.Canceled), context, ct).ConfigureAwait(false);
            if (!settled.IsSuccess)
                return settled;
        }

        return SourceUseReceiverSupport.Superseded(reason, evidence);
    }
}

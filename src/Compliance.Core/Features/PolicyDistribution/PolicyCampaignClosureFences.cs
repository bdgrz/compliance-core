using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

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

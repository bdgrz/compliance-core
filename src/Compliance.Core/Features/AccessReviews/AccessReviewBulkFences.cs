using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Ends a batch that can definitely never publish: the batch cancellation is accepted first, which fences every
///     delayed hold and apply of the generation, and only then are the item fences completed and the item uses settled
///     (by the cancellation reactors, even for uses that were never acquired). The cancel uses the deterministic command
///     id the process driver would use, so a later driver step replays it. A batch that already published recovers
///     forward. Nothing is inferred from missing native state.
/// </summary>
sealed class AccessReviewBulkFences(IRequestBus bus, IImmutableContentStore content)
{
    public async ValueTask<Result<ReceiverOutcome>> CancelAsync(AccessReviewBulk bulk, NativeFactReference source,
        string reason, Uuid? itemId, IExecutionContext context, CancellationToken ct)
    {
        var (campaign, admitted, intent) = (bulk.Campaign!, bulk.Admitted!, bulk.Intent!);
        var tenantId = Uuid.Parse(campaign.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture);
        var proof = await AccessReviewContent.WriteAsync(content, tenantId, AccessReviewContent.BulkFailureProofSchema,
            new AccessReviewBulkFailure(reason, itemId, NativeFactReference.Of(campaign.Stream, admitted)), ct)
            .ConfigureAwait(false);
        var cancelId = AccessReviewEffects.ReceiverCommandId(AccessReviewEffects.BulkProcessName, source.EventId,
            nameof(CancelAccessReviewDecisionBatch), null, intent.Header.Episode);
        var canceled = await bus.SendAsync(new CancelAccessReviewDecisionBatch(cancelId,
            AccessReviewEffects.BatchStream(tenantId, intent.CampaignId, intent.DecisionBatchId), intent.CampaignId,
            intent.DecisionBatchId, AccessReviewEffects.Generation, 0, proof), context, ct).ConfigureAwait(false);
        if (!canceled.IsSuccess)
            return canceled;
        // A batch that already published recovers forward: its own settlement makes the decisions visible.
        if (canceled.Value is ReceiverOutcome.Superseded)
            return canceled;
        var fact = canceled.Value switch
        {
            ReceiverOutcome.Applied applied => applied.Fact,
            ReceiverOutcome.Replayed replayed => replayed.Fact,
            _ => null,
        };
        return SourceUseReceiverSupport.Superseded(reason, fact);
    }
}

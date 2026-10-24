using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Ends a pending completion that can definitely never close: the campaign cancellation is appended first, which
///     fences the closure and every delayed acquisition against late delivery, and only then are the item uses
///     settled (by the settlement reactor, even for uses that were never acquired, so a delayed acquisition is
///     refused). The abort uses the deterministic command id the process driver would use, so a later driver step
///     replays it. Nothing is inferred from missing native state.
/// </summary>
sealed class AccessReviewCompletionFences(IRequestBus bus, IImmutableContentStore content)
{
    public async ValueTask<Result<ReceiverOutcome>> AbortAsync(AccessReviewCompletion completion,
        NativeFactReference source, string reason, Uuid? itemId, IExecutionContext context, CancellationToken ct)
    {
        var (campaign, accepted, intent) = (completion.Campaign!, completion.Accepted!, completion.Intent!);
        var tenantId = Uuid.Parse(campaign.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture);
        var proof = await AccessReviewContent.WriteAsync(content, tenantId, AccessReviewContent.FailureProofSchema,
            new AccessReviewCompletionFailure(reason, itemId, NativeFactReference.Of(campaign.Stream, accepted)), ct)
            .ConfigureAwait(false);
        var abortId = AccessReviewEffects.ReceiverCommandId(AccessReviewEffects.CompletionProcessName, source.EventId,
            nameof(AbortAccessReviewCampaignCompletion), null, intent.Header.Episode);
        var aborted = await bus.SendAsync(new AbortAccessReviewCampaignCompletion(abortId,
            intent.Header.RootOperationId, campaign.Stream, AccessReviewEffects.Generation, accepted.Revision, proof),
            context, ct).ConfigureAwait(false);
        if (!aborted.IsSuccess)
            return aborted;
        // A completion that already closed recovers forward: its own settlement installs the closure fence.
        if (aborted.Value is ReceiverOutcome.Superseded)
            return aborted;
        var fact = aborted.Value switch
        {
            ReceiverOutcome.Applied applied => applied.Fact,
            ReceiverOutcome.Replayed replayed => replayed.Fact,
            _ => null,
        };
        return SourceUseReceiverSupport.Superseded(reason, fact);
    }
}

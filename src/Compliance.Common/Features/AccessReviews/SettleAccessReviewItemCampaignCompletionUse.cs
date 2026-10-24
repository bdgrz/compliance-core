using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Settles the item's completion use after the campaign closed (<c>parent_closed</c>, which installs the closure
///     fence) or after the cancellation fence (<c>canceled</c>), even when the use was never acquired so a delayed
///     acquisition is refused. <see cref="Source" /> is the accepted completion fact; the receiver resolves the
///     terminal fact from the campaign and never infers a cancellation from missing native state.
/// </summary>
[Discriminator("bdgrz.access-review-item.settle-campaign-completion-use", 1)]
public sealed record SettleAccessReviewItemCampaignCompletionUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid CampaignId, SourceUseSettlementDisposition Disposition)
    : ISourceUseSettlement, IAccessReviewReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

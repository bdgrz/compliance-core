using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Materializes one frozen item of the <c>AccessReviewLaunchFanOut</c>. <paramref name="Source" /> is the
///     <see cref="AccessReviewCampaignLaunched" /> fact whose launch manifest carries the item and
///     <paramref name="Intent" /> its frozen creation intent. Items are independent of one another.
/// </summary>
[Discriminator("bdgrz.access-review-item.materialize", 1)]
public sealed record MaterializeAccessReviewItem(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision,
    ImmutableContentRef Intent, Uuid CampaignId, Uuid ItemId)
    : IRequest<ReceiverOutcome>, IAccessReviewReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

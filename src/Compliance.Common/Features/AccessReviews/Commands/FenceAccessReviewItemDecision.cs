using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Fences one item against a canceled batch, including an item the batch never held, so a delayed hold or apply
///     is refused before any hold is released. <paramref name="Source" /> is the batch's
///     <see cref="AccessReviewDecisionBatchCancellationAccepted" /> fact.
/// </summary>
[Discriminator("bdgrz.access-review-item.fence-decision", 1)]
public sealed record FenceAccessReviewItemDecision(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision,
    ImmutableContentRef Intent, Uuid CampaignId, Uuid ItemId, Uuid DecisionBatchId)
    : IRequest<ReceiverOutcome>, IAccessReviewReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

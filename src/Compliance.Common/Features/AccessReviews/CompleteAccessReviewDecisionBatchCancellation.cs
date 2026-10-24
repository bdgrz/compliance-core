using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Completes the batch cancellation once every selected item carries its fence.
///     <paramref name="ItemFenceProofs" /> is the complete set of <see cref="BulkItemFenceProof" />; the receiver
///     verifies each against the item streams.
/// </summary>
[Discriminator("bdgrz.access-review-decision-batch.complete-cancellation", 1)]
public sealed record CompleteAccessReviewDecisionBatchCancellation(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision,
    ImmutableContentRef Intent, Uuid DecisionBatchId, ImmutableContentRef ItemFenceProofs)
    : IRequest<ReceiverOutcome>, IAccessReviewReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

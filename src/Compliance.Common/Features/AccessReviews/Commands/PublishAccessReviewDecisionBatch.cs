using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Publishes every provisional decision of the batch as one fact. <paramref name="ItemDecisionProofs" /> is the
///     complete set of <see cref="BulkItemDecisionProof" /> (exact item event and position); the receiver verifies
///     each against the item streams and refuses a partial or changed set.
/// </summary>
[Discriminator("bdgrz.access-review-decision-batch.publish", 1)]
public sealed record PublishAccessReviewDecisionBatch(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision,
    ImmutableContentRef Intent, Uuid DecisionBatchId, ImmutableContentRef ItemDecisionProofs)
    : IRequest<ReceiverOutcome>, IAccessReviewReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

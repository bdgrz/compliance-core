using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Materializes the decision batch from the admitted bulk decision. <paramref name="Source" /> is the
///     <see cref="AccessReviewDecisionBatchAdmitted" /> fact and <paramref name="Intent" /> its frozen
///     <see cref="BulkAccessDecisionIntent" />; the batch starts at its own revision zero and is never compared with
///     the campaign revision.
/// </summary>
[Discriminator("bdgrz.access-review-decision-batch.materialize", 1)]
public sealed record MaterializeAccessReviewDecisionBatch(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision,
    ImmutableContentRef Intent, Uuid CampaignId, Uuid DecisionBatchId)
    : IRequest<ReceiverOutcome>, IAccessReviewReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Trusted internal only. Cancels a batch that can never publish (a permanent preview or eligibility failure) or
///     whose cancellation was explicitly authorized, before any irreversible publication. A published batch recovers
///     forward and is never canceled. Every item is fenced before any hold is settled.
///     <paramref name="Target" /> names the batch stream, which carries the tenant realm.
/// </summary>
[Discriminator("bdgrz.access-review-decision-batch.cancel", 1)]
public sealed record CancelAccessReviewDecisionBatch(Uuid CommandId, EventStreamAddress Target, Uuid CampaignId,
    Uuid DecisionBatchId, long Generation, long ExpectedRevision, ImmutableContentRef FailureProof)
    : IRequest<ReceiverOutcome>, IAccessReviewReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

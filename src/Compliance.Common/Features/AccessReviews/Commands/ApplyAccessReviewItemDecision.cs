using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     Applies the batch's decision to one held item as a provisional fact that stays hidden until the batch
///     publishes. The item must still hold the batch's reservation and must not be fenced.
/// </summary>
[Discriminator("bdgrz.access-review-item.apply-decision", 1)]
public sealed record ApplyAccessReviewItemDecision(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision,
    ImmutableContentRef Intent, Uuid CampaignId, Uuid ItemId, Uuid DecisionBatchId)
    : IRequest<ReceiverOutcome>, IAccessReviewReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

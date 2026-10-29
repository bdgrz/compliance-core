using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     Holds the exact head of one item that the bulk decision froze at preview (the generic source-use acquisition
///     with the <c>bulk_decision</c> purpose). The item refuses every other change while the hold is active, and an
///     item whose head moved, or that is closed or fenced, rejects and cancels the batch.
///     <see cref="Source" /> is the admitted bulk decision; <see cref="SourceOwnerId" /> is the item id.
/// </summary>
[Discriminator("bdgrz.access-review-item.reserve-decision", 1)]
public sealed record ReserveAccessReviewItemDecision(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid ItemId, Uuid SourceContentEventId, ulong SourceRevision, Uuid CampaignId,
    Uuid DecisionBatchId)
    : ISourceUseAuthorization, IAccessReviewReceiver
{
    [System.Text.Json.Serialization.JsonIgnore]
    public Uuid SourceOwnerId => ItemId;

    [System.Text.Json.Serialization.JsonIgnore]
    public ulong ExpectedContentRevision => SourceRevision;

    Uuid IIdentifiedRequest.RequestId => CommandId;
}

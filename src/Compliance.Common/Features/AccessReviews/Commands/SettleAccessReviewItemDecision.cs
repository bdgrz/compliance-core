using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     Settles the item's bulk decision use after the batch published (<c>irreversible_decision</c>, which makes the
///     item's provisional decision visible) or after the item fence of a canceled batch (<c>canceled</c>), even when
///     the use was never acquired so a delayed hold is refused. The receiver resolves the terminal fact from the
///     batch and never infers a cancellation from missing native state.
/// </summary>
[Discriminator("bdgrz.access-review-item.settle-decision", 1)]
public sealed record SettleAccessReviewItemDecision(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid ItemId, Uuid CampaignId, Uuid DecisionBatchId, SourceUseSettlementDisposition Disposition)
    : ISourceUseSettlement, IAccessReviewReceiver
{
    [System.Text.Json.Serialization.JsonIgnore]
    public Uuid SourceOwnerId => ItemId;

    Uuid IIdentifiedRequest.RequestId => CommandId;
}

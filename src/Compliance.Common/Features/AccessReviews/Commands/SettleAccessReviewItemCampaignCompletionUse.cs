using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     Settles the item's completion use after the campaign closed (<c>parent_closed</c>, which installs the closure
///     fence) or after the cancellation fence (<c>canceled</c>), even when the use was never acquired so a delayed
///     acquisition is refused. <see cref="Source" /> is the accepted completion fact; the receiver resolves the
///     terminal fact from the campaign and never infers a cancellation from missing native state.
/// </summary>
[Discriminator("bdgrz.access-review-item.settle-campaign-completion-use", 1)]
public sealed record SettleAccessReviewItemCampaignCompletionUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid ReceivingOwnerId, SourceUseSettlementDisposition Disposition)
    : ISourceUseSettlement, IAccessReviewReceiver
{
    /// <summary>The receiving owner whose exact accepted operation this use serves.</summary>
    [JsonIgnore]
    public Uuid CampaignId => ReceivingOwnerId;

    Uuid IIdentifiedRequest.RequestId => CommandId;
}

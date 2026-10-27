using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Holds the exact pinned head of one item for a campaign completion. The item refuses every change while the
///     use is held. <see cref="Source" /> is the accepted completion fact; the receiver verifies it by hydrating the
///     campaign. <see cref="SourceOwnerId" /> is the item id.
/// </summary>
[Discriminator("bdgrz.access-review-item.authorize-campaign-completion-use", 1)]
public sealed record AuthorizeAccessReviewItemCampaignCompletionUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid SourceContentEventId, long SourceRevision, Uuid ReceivingOwnerId)
    : ISourceUseAuthorization, IAccessReviewReceiver
{
    /// <summary>The source revision in the shared use protocol; invalid negative input cannot match a held revision.</summary>
    [JsonIgnore]
    public ulong ExpectedContentRevision => SourceRevision >= 0 ? (ulong)SourceRevision : ulong.MaxValue;

    /// <summary>The receiving owner whose exact accepted operation this use serves.</summary>
    [JsonIgnore]
    public Uuid CampaignId => ReceivingOwnerId;

    Uuid IIdentifiedRequest.RequestId => CommandId;
}

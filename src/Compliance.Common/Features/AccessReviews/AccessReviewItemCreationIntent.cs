using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The complete frozen terms of one item creation, taken from the launch manifest. The stable item identity, the
///     original reviewer and the reviewer episode are frozen here. <paramref name="PopulationEntry" /> is the item's
///     <see cref="AccessReviewItemView" /> as verified content; <paramref name="SnapshotProof" /> names the frozen
///     launch snapshot the entry was taken from.
/// </summary>
public sealed record AccessReviewItemCreationIntent(DurableIntentHeader Header, Uuid CampaignId,
    ulong CampaignGeneration, Uuid ItemId, Uuid SystemInstanceId, ImmutableContentRef PopulationEntry,
    Uuid ReviewerMemberId, Uuid ReviewerEpisodeId, NativeFactReference SnapshotProof)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "access-review-item-creation-intent.v1";
}

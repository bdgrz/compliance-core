using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A campaign stream and one item of it, with the item's frozen view and folded state when the campaign has the item.</summary>
public sealed record AccessReviewItemRead(AccessReviewCampaign Campaign, AccessReviewItem Item,
    AccessReviewItemView? Frozen, AccessReviewItemState? State, AccessReviewItemHistorySnapshot? History = null);

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>Campaign launch identity used to validate replay of its reviewed-system index.</summary>
public sealed record AccessReviewCampaignImpactSummaryView(Uuid TenantId,
    AccessReviewCampaignSummaryView Summary, string ContentSha256,
    IReadOnlyList<Uuid> SystemInstanceIds);

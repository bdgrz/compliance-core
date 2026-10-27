using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Queries;

/// <summary>Lists campaigns, newest launch first.</summary>
[Discriminator("bdgrz.access_review.campaign.list", 1)]
public sealed record ListAccessReviewCampaigns(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AccessReviewCampaignSummaryView>>, IAccessReviewRequest, ICallable;

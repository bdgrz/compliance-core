using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Queries;

/// <summary>
///     Reads a campaign. A member without the access-review permission sees only the items
///     assigned to them, and nothing when none are.
/// </summary>
[Discriminator("bdgrz.access_review.campaign.get", 1)]
public sealed record GetAccessReviewCampaign(Uuid TenantId, Uuid CampaignId)
    : IRequest<AccessReviewCampaignView>, IAccessReviewParticipantRequest, ICallable;

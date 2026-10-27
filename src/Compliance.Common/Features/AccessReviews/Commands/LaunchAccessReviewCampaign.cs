using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     Launches a campaign over accepted populations, freezing its items, reviewers, instructions,
///     and deadline as an immutable snapshot. <paramref name="CampaignId" /> is the logical operation
///     identity and the campaign identity; an exact retry replays the original registration.
/// </summary>
[Discriminator("bdgrz.access_review.campaign.launch", 2)]
public sealed record LaunchAccessReviewCampaign(Uuid CampaignId, Uuid TenantId, string Name,
    string Instructions, DateTimeOffset Deadline,
    IReadOnlyList<AccessReviewAssignment> Assignments,
    Uuid? ProgramId = null, Uuid? RemediationOwnerMemberId = null)
    : IRequest<AccessReviewCampaignRegistration>, IAccessReviewMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CampaignId;
}

using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>Records an audited source-level replacement for an ineligible campaign responsibility.</summary>
[Discriminator("bdgrz.access_review.responsibility.reassign", 2)]
public sealed record ReassignAccessReviewResponsibility(Uuid CommandId, Uuid TenantId, Uuid CampaignId,
    Uuid ItemId, string Responsibility, long ExpectedRevision, Uuid AssignedMemberId,
    string Reason, string? DelegationReason = null) : IRequest<AccessReviewResponsibilityReassignmentView>,
    IAccessReviewMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

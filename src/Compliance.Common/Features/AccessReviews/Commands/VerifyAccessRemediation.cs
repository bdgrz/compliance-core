using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>Verifies remediation only when a later accepted population shows the access removed or changed.</summary>
[Discriminator("bdgrz.access_review.remediation.verify", 2)]
public sealed record VerifyAccessRemediation(Uuid CommandId, Uuid TenantId, Uuid CampaignId,
    Uuid ItemId, long ExpectedRevision, Uuid PopulationId)
    : IRequest<AccessRemediationVerificationView>, IAccessReviewMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

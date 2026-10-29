using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>Records a provider-side change for a modify or revoke decision; it does not verify remediation.</summary>
[Discriminator("bdgrz.access_review.remediation.change.record", 2)]
public sealed record RecordAccessRemediationChange(Uuid CommandId, Uuid TenantId, Uuid CampaignId,
    Uuid ItemId, long ExpectedRevision, string Reference, string Description,
    DateTimeOffset ChangedAt)
    : IRequest<AccessRemediationChangeView>, IAccessReviewMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

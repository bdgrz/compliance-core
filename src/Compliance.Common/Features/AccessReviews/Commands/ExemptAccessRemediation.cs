using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     A personal approval that a required remediation may remain unverified. The item's reviewer
///     cannot approve it. HTTP-only; it is not registered as an MCP tool.
/// </summary>
[Discriminator("bdgrz.access_review.remediation.exception.record", 2)]
public sealed record ExemptAccessRemediation(Uuid CommandId, Uuid TenantId, Uuid CampaignId,
    Uuid ItemId, long ExpectedRevision, string Rationale, DateTimeOffset? ExpiresAt = null)
    : IRequest<AccessRemediationExceptionView>, IAccessReviewMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     The assigned reviewer's personal decision on one item. A reviewer may not decide their own
///     access without an exact-scope waiver. <paramref name="ExpectedRevision" /> is the item's own revision.
///     HTTP-only; it is not registered as an MCP tool.
/// </summary>
[Discriminator("bdgrz.access_review.decision.record", 2)]
public sealed record RecordAccessDecision(Uuid CommandId, Uuid TenantId, Uuid CampaignId, Uuid ItemId,
    long ExpectedRevision, string Decision, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<AccessDecisionView>, IAccessReviewParticipantRequest, IAccessReviewMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

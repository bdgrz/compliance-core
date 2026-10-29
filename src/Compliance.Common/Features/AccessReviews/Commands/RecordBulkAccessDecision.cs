using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     Records one shared-rationale decision on exactly the previewed eligible items as one atomic batch.
///     <paramref name="DecisionBatchId" /> is the logical operation identity and the batch identity;
///     <paramref name="ExpectedCampaignRevision" /> is the revision of the campaign that first admits the complete
///     preview and selection (never the revision of the batch, which starts at zero). The request returns the fixed
///     202 acknowledgment; the independent batch owner then holds every selected item at the head the preview froze
///     and publishes all decisions at once. HTTP-only; it is not registered as an MCP tool.
/// </summary>
[Discriminator("bdgrz.access_review.decision.bulk_record", 2)]
public sealed record RecordBulkAccessDecision(Uuid DecisionBatchId, Uuid TenantId, Uuid CampaignId,
    IReadOnlyList<Uuid> ItemIds, string Decision, string Rationale, string PreviewToken, long ExpectedCampaignRevision)
    : IRequest<OperationAcceptedResponse>, IAccessReviewParticipantRequest, IAccessReviewMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => DecisionBatchId;
}

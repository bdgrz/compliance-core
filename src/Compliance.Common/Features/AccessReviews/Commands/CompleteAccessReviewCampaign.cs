using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     The owner's personal completion sign-off. It admits the completion on the campaign against
///     <paramref name="ExpectedRevision" /> and returns the fixed 202 acknowledgment; the completion process
///     then holds every item, freezes the final campaign snapshot and closes the campaign. It is refused while
///     any item is unresolved or any required remediation is unverified without an approved exception.
///     HTTP-only; it is not registered as an MCP tool.
/// </summary>
[Discriminator("bdgrz.access_review.campaign.complete", 2)]
public sealed record CompleteAccessReviewCampaign(Uuid CommandId, Uuid TenantId, Uuid CampaignId,
    long ExpectedRevision, string Attestation)
    : IRequest<OperationAcceptedResponse>, IAccessReviewMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

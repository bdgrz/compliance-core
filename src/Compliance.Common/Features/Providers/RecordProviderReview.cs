using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Requests the acting member's personal due-diligence conclusion. <paramref name="ReviewId" /> is the logical
///     identity; admission freezes the review and answers 202, and the review process records it once the exact
///     report revision is held. HTTP-only: it is deliberately not an MCP tool, so a machine cannot sign a review.
/// </summary>
[Discriminator("bdgrz.provider.review.record", 1)]
public sealed record RecordProviderReview(Uuid ReviewId, Uuid TenantId, Uuid ProviderId, ProviderReviewContent Content)
    : IRequest<OperationAcceptedResponse>, IProviderManagementRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => ReviewId;
}

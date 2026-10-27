using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Trusted internal only. Cancels a pending completion whose authoritative proof permanently mismatches before the
///     irreversible closure, and fences every delayed local finalization of the generation. A completed closure
///     recovers forward and is never canceled. Source uses are settled only after this fence.
/// </summary>
[Discriminator("bdgrz.access-review-campaign.abort-completion", 1)]
public sealed record AbortAccessReviewCampaignCompletion(Uuid CommandId, Uuid OperationId, EventStreamAddress Target,
    long Generation, long ExpectedRevision, ImmutableContentRef FailureProof)
    : IRequest<ReceiverOutcome>, IAccessReviewReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

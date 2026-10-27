using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Commands;

/// <summary>
///     Closes the campaign after every item was held. <paramref name="Intent" /> is the frozen
///     <see cref="ClosureIntent" /> and <paramref name="ItemProofs" /> the complete expected proof set it pins; the
///     receiver verifies each item's exact grant and current decision, remediation and exception proof under the
///     holds before it appends the completion.
/// </summary>
[Discriminator("bdgrz.access-review-campaign.complete-closure", 1)]
public sealed record CompleteAccessReviewCampaignClosure(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid CampaignId, ImmutableContentRef ItemProofs)
    : IRequest<ReceiverOutcome>, IAccessReviewReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

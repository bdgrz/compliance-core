using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The campaign admitted the owner's completion: the exact campaign revision, the frozen
///     <see cref="ClosureIntent" /> (the attestation, the complete drained item selection and the pinned head of
///     every item) and the original 202 acknowledgment. The completion process holds every item from it.
/// </summary>
[Discriminator("bdgrz.access-review-campaign.access-review-campaign-completion-accepted", 1)]
public sealed record AccessReviewCampaignCompletionAccepted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

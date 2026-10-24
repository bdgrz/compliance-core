using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The pending completion was canceled and its generation fenced against every delayed finalization, after an
///     authoritative permanent proof mismatch before the irreversible closure. <paramref name="SourceProofs" /> is
///     the failure proof.
/// </summary>
[Discriminator("bdgrz.access-review-campaign.access-review-campaign-completion-canceled", 1)]
public sealed record AccessReviewCampaignCompletionCanceled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

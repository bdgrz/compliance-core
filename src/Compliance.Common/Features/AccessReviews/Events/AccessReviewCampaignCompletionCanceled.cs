using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Events;

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

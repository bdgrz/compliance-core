using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     The campaign was completed with its final immutable snapshot, after every item was held and its exact
///     decision, remediation and exception proof was verified. <paramref name="ItemCompletionProofs" /> is the
///     complete proof set; <paramref name="OperationResult" /> is the original typed completion result.
/// </summary>
[Discriminator("bdgrz.access_review.campaign.completed", 2)]
public sealed record AccessReviewCampaignCompleted(Uuid TenantId, Uuid CampaignId, long Revision,
    ImmutableContentRef CompletionContent, OperationProvenance Operation, long CampaignGeneration,
    ImmutableContentRef ItemCompletionProofs, ImmutableContentRef Intent, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

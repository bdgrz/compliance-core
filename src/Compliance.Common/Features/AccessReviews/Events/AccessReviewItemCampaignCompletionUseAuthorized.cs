using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     The exact pinned head of one item is held for a campaign completion. The use does not advance the item's
///     public revision; <paramref name="Revision" /> is the held revision.
/// </summary>
[Discriminator("bdgrz.access-review-item.access-review-item-campaign-completion-use-authorized", 1)]
public sealed record AccessReviewItemCampaignCompletionUseAuthorized(Uuid TenantId, Uuid CampaignId, Uuid ItemId,
    EventStreamAddress Owner, long Revision, long Generation, ImmutableContentRef Intent,
    ImmutableContentRef? SourceProofs, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;

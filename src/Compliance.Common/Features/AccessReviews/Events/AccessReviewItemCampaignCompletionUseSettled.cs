using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     The campaign completion use of one item ended. <paramref name="Disposition" /> is the one the verified terminal
///     campaign fact requires: <c>parent_closed</c> keeps the item closed against every later change (the closure
///     fence), <c>canceled</c> clears only this use after the parent cancellation fence. A settlement before any
///     grant fences the key against late acquisition.
/// </summary>
[Discriminator("bdgrz.access-review-item.access-review-item-campaign-completion-use-settled", 1)]
public sealed record AccessReviewItemCampaignCompletionUseSettled(Uuid TenantId, Uuid CampaignId, Uuid ItemId,
    EventStreamAddress Owner, long Revision, long Generation, ImmutableContentRef Intent,
    ImmutableContentRef? SourceProofs, SourceUseSettlementDisposition Disposition, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;

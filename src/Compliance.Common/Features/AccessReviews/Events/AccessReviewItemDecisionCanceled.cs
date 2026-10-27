using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The item's fence against a canceled bulk decision batch: from this fact a delayed hold or apply of the batch is
///     refused, including for an item that was never held. It does not advance the item's revision and clears no
///     hold; the matching settlement does that afterwards.
/// </summary>
[Discriminator("bdgrz.access-review-item.access-review-item-decision-canceled", 1)]
public sealed record AccessReviewItemDecisionCanceled(Uuid TenantId, Uuid CampaignId, Uuid ItemId,
    EventStreamAddress Owner, long Revision, long Generation, ImmutableContentRef Intent,
    ImmutableContentRef? SourceProofs, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     The exact frozen head of one item is held for a bulk decision batch. The use does not advance the item's
///     revision; <paramref name="Revision" /> is the held revision. The item refuses every other change while the use
///     is held.
/// </summary>
[Discriminator("bdgrz.access-review-item.access-review-item-decision-authorized", 1)]
public sealed record AccessReviewItemDecisionAuthorized(Uuid TenantId, Uuid CampaignId, Uuid ItemId,
    EventStreamAddress Owner, long Revision, long Generation, ImmutableContentRef Intent,
    ImmutableContentRef? SourceProofs, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;

using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Events;

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

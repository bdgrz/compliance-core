using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     The bulk decision use of one item ended. <paramref name="Disposition" /> is the one the verified terminal batch
///     fact requires: <c>irreversible_decision</c> after the batch published, which makes the item's provisional
///     decision of the batch visible, or <c>canceled</c> after the item fence, which keeps it hidden forever. A
///     settlement before any hold fences the key against a late hold.
/// </summary>
[Discriminator("bdgrz.access-review-item.access-review-item-decision-settled", 1)]
public sealed record AccessReviewItemDecisionSettled(Uuid TenantId, Uuid CampaignId, Uuid ItemId,
    EventStreamAddress Owner, long Revision, long Generation, ImmutableContentRef Intent,
    ImmutableContentRef? SourceProofs, SourceUseSettlementDisposition Disposition, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;

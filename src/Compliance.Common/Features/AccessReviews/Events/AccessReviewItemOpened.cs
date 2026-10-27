using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     One frozen item was materialized from the launch manifest of its campaign. The item's logical revision is 1.
///     <paramref name="Intent" /> is the frozen <c>AccessReviewItemCreationIntent</c>.
/// </summary>
[Discriminator("bdgrz.access-review-item.access-review-item-opened", 1)]
public sealed record AccessReviewItemOpened(Uuid TenantId, Uuid CampaignId, Uuid ItemId, EventStreamAddress Owner,
    long Revision, long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

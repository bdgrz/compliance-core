using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     Every selected item was fenced and the batch is canceled; no decision of it is ever published.
///     <paramref name="SourceProofs" /> is the complete set of <see cref="BulkItemFenceProof" />.
/// </summary>
[Discriminator("bdgrz.access-review-decision-batch.access-review-decision-batch-canceled", 1)]
public sealed record AccessReviewDecisionBatchCanceled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

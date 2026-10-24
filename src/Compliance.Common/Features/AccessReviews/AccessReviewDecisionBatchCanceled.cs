using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Every selected item was fenced and the batch is canceled; no decision of it is ever published.
///     <paramref name="SourceProofs" /> is the complete set of <see cref="BulkItemFenceProof" />.
/// </summary>
[Discriminator("bdgrz.access-review-decision-batch.access-review-decision-batch-canceled", 1)]
public sealed record AccessReviewDecisionBatchCanceled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

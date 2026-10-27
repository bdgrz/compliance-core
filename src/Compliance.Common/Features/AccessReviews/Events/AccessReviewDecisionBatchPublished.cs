using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     Every selected item held its provisional decision and the batch published all of them as one fact.
///     <paramref name="SourceProofs" /> is the complete set of <see cref="BulkItemDecisionProof" /> (exact item event
///     and position); <paramref name="OperationResult" /> is the terminal <see cref="BulkAccessDecisionResult" />.
/// </summary>
[Discriminator("bdgrz.access-review-decision-batch.access-review-decision-batch-published", 1)]
public sealed record AccessReviewDecisionBatchPublished(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

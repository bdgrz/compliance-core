using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     The batch owner materialized the admitted batch at logical revision zero from the exact admitted
///     <see cref="BulkAccessDecisionIntent" />. Nothing is reconstructed from current campaign or item state.
/// </summary>
[Discriminator("bdgrz.access-review-decision-batch.access-review-decision-batch-accepted", 1)]
public sealed record AccessReviewDecisionBatchAccepted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

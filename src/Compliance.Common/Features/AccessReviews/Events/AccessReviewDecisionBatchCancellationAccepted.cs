using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     The batch accepted a cancellation before any irreversible publication. From this fact every selected item is
///     fenced against delayed holds and applies before any hold is released. <paramref name="SourceProofs" /> is the
///     failure proof.
/// </summary>
[Discriminator("bdgrz.access-review-decision-batch.access-review-decision-batch-cancellation-accepted", 1)]
public sealed record AccessReviewDecisionBatchCancellationAccepted(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

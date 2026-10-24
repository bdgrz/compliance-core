using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     A pending completion review ended before it recorded its decision. The fact is the local receiver fence: a
///     delayed <see cref="CompleteRiskTreatmentActionReview" /> sees it and does nothing, and only then are the
///     evidence uses settled. <paramref name="SourceProofs" /> is the failure proof.
/// </summary>
[Discriminator("bdgrz.risk-treatment-action.risk-treatment-action-completion-review-canceled", 1)]
public sealed record RiskTreatmentActionCompletionReviewCanceled(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

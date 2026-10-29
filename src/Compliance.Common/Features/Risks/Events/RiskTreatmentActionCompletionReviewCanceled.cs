using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Commands;
namespace Bdgrz.Compliance.Features.Risks.Events;

/// <summary>
///     A pending completion review ended before it recorded its decision. The fact is the local receiver fence: a
///     delayed <see cref="CompleteRiskTreatmentActionReview" /> sees it and does nothing, and only then are the
///     evidence uses settled. <paramref name="SourceProofs" /> is the failure proof.
/// </summary>
[Discriminator("bdgrz.risk-treatment-action.risk-treatment-action-completion-review-canceled", 1)]
public sealed record RiskTreatmentActionCompletionReviewCanceled(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

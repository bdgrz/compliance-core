using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Events;

/// <summary>
///     The root native acceptance of a completion review. It seals the exact submission for <c>Generation</c>, which is
///     the submission ordinal of the action, and references the frozen <see cref="RiskActionCompletionReviewIntent" />;
///     the action only completes or reopens once <see cref="RiskTreatmentActionCompletionReviewed" /> follows.
/// </summary>
[Discriminator("bdgrz.risk-treatment-action.risk-treatment-action-completion-review-accepted", 1)]
public sealed record RiskTreatmentActionCompletionReviewAccepted(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

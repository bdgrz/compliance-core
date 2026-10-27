using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Events;

/// <summary>
///     The decision of a completion review, appended by <c>RiskActionCompletionReviewProcess</c> once every cited
///     evidence request is held. <paramref name="Intent" /> is the frozen <see cref="RiskActionCompletionReviewIntent" />.
/// </summary>
[Discriminator("bdgrz.risk.treatment_action.completion_reviewed", 2)]
public sealed record RiskTreatmentActionCompletionReviewed(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision,
    Uuid ActionId, Uuid DecisionId, string Outcome, string Rationale, ActorReference Reviewer,
    DateTimeOffset ReviewedAt, Uuid? SeparationOfDutiesWaiverId, OperationProvenance Operation,
    ImmutableContentRef Intent, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

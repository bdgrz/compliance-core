using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     The independent review of the pending proposal <paramref name="DecisionId" />. The edition and criterion name
///     the applicability line the fact belongs to; <paramref name="ReviewDecisionId" /> is the logical id of the
///     review operation.
/// </summary>
[Discriminator("bdgrz.criterion_applicability.reviewed", 2)]
public sealed record CriterionApplicabilityReviewed(Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision,
    int VersionNumber, Uuid ReviewDecisionId, string Outcome, string Rationale, ActorReference Actor,
    DateTimeOffset ReviewedAt, Uuid? SeparationOfDutiesWaiverId, Uuid EditionId, string CriterionIdentifier,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

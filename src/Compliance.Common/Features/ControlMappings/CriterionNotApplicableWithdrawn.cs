using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     The withdrawal of the accepted not-applicable decision <paramref name="DecisionId" />. The edition and
///     criterion name the applicability line the fact belongs to.
/// </summary>
[Discriminator("bdgrz.criterion_applicability.not_applicable_withdrawn", 2)]
public sealed record CriterionNotApplicableWithdrawn(Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision,
    int VersionNumber, string Rationale, ActorReference Actor, DateTimeOffset WithdrawnAt, Uuid EditionId,
    string CriterionIdentifier, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

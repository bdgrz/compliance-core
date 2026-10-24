using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     A pending not-applicable proposal on the applicability line of one (edition, criterion). The logical id of
///     <paramref name="Operation" /> is <paramref name="DecisionId" />; <paramref name="Revision" /> counts the facts
///     of the criterion's line only.
/// </summary>
[Discriminator("bdgrz.criterion_applicability.not_applicable_proposed", 2)]
public sealed record CriterionNotApplicableProposed(Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision,
    int VersionNumber, Uuid EditionId, string CriterionIdentifier, string Rationale, Uuid ProposerMemberId,
    ActorReference Actor, DateTimeOffset ProposedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

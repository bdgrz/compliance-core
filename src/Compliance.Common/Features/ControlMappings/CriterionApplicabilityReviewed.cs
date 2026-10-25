using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

[Discriminator("bdgrz.criterion_applicability.reviewed", 3)]
public sealed record CriterionApplicabilityReviewed(Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision,
    int VersionNumber, Uuid ReviewDecisionId, string Outcome, ActorReference Actor,
    DateTimeOffset ReviewedAt, Uuid? SeparationOfDutiesWaiverId, Uuid EditionId, string CriterionIdentifier,
    ImmutableContentRef Content, MappingLineHeader Header, OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, IMappingLineFact;

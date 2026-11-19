using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>A proposed value for one field or independently owned disclosure.</summary>
[Discriminator("bdgrz.criteria.edition-value-proposed", 1)]
public sealed record CriteriaEditionValueProposed(CriteriaEditionValueIntent Intent, long ExecutionGeneration,
    Uuid EditionRequestEventId, Uuid OperationClaimEventId, ActorAttribution Actor,
    AccessAdmissionEvidence Admission) : DomainEvent;

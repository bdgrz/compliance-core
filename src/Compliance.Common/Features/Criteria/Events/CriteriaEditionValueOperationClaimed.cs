using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>Immutable ownership of one target-scoped authored operation.</summary>
[Discriminator("bdgrz.criteria.edition-value-operation-claimed", 1)]
public sealed record CriteriaEditionValueOperationClaimed(CriteriaEditionValueIntent Intent,
    long ExecutionGeneration, ActorAttribution Actor, AccessAdmissionEvidence Admission) : DomainEvent;

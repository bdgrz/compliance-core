using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>An admitted structural intent for one independently owned entry.</summary>
[Discriminator("bdgrz.criteria.entry-change-proposed", 1)]
public sealed record CriterionChangeProposed(CriterionChangeIntent Intent, long ExecutionGeneration,
    Uuid IncarnationId, ActorAttribution Actor, AccessAdmissionEvidence Admission,
    Uuid? ParentAcceptedEventId, Uuid OperationClaimEventId) : DomainEvent;

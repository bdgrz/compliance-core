using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-edition-draft-request-cancelled", 1)]
public sealed record CriteriaEditionDraftRequestCancelled(Uuid EditionId, Uuid OperationId, long ExecutionGeneration, Uuid TargetOperationId, ActorAttribution Actor, AccessAdmissionEvidence Admission) : DomainEvent;

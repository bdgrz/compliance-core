using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-edition-rename-cancelled", 1)]
public sealed record CriteriaEditionRenameCancelled(Uuid EditionId, Uuid OperationId, long ExecutionGeneration, Uuid RenameOperationId, ActorAttribution Actor, AccessAdmissionEvidence Admission) : DomainEvent;

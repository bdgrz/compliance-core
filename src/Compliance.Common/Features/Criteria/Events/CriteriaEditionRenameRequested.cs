using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-edition-rename-requested", 1)]
public sealed record CriteriaEditionRenameRequested(Uuid EditionId, Uuid OperationId, long ExecutionGeneration, string OldLabel, long OldLabelGeneration, string NewLabel, ActorAttribution Actor, AccessAdmissionEvidence Admission) : DomainEvent;

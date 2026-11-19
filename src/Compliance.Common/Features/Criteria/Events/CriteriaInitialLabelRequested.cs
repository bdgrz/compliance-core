using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-initial-label-requested", 1)]
public sealed record CriteriaInitialLabelRequested(Uuid EditionId, Uuid OperationId, long ExecutionGeneration, string Label, ActorAttribution Actor, AccessAdmissionEvidence Admission) : DomainEvent;

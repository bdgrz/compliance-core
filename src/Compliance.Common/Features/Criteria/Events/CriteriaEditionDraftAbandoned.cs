using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-edition-draft-abandoned", 1)]
public sealed record CriteriaEditionDraftAbandoned(Uuid EditionId, Uuid DraftGeneration, Uuid OperationId, long ExecutionGeneration, string Label, long LabelGeneration, ActorAttribution Actor, AccessAdmissionEvidence Admission) : DomainEvent;

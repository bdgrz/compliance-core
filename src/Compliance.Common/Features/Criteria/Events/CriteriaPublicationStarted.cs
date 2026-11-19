using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-publication-started", 1)]
public sealed record CriteriaPublicationStarted(Uuid EditionId, Uuid DraftGeneration, Uuid PublicationAttemptId, long ExecutionGeneration, long AttemptOrdinal, string WitnessIdentifier, Uuid WitnessIncarnationId, string Label, long LabelGeneration, Uuid LabelConfirmationEventId, ActorAttribution Actor, AccessAdmissionEvidence? Admission) : DomainEvent;

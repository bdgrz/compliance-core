using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-publication-abort-cleanup-completed", 1)]
public sealed record CriteriaPublicationAbortCleanupCompleted(Uuid EditionId, Uuid DraftGeneration, Uuid PublicationAttemptId, long AttemptOrdinal, string WitnessIdentifier, Uuid WitnessIncarnationId, Uuid WitnessFenceEventId) : DomainEvent;

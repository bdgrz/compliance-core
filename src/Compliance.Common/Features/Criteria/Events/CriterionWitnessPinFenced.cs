using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>An aborted publication attempt can never acquire or retain this witness.</summary>
[Discriminator("bdgrz.criteria.witness-pin-fenced", 1)]
public sealed record CriterionWitnessPinFenced(Uuid EditionId, Uuid DraftGeneration, string Identifier,
    Uuid PublicationAttemptId, long AttemptOrdinal, Uuid IncarnationId, Uuid PublicationAbortedEventId) : DomainEvent;

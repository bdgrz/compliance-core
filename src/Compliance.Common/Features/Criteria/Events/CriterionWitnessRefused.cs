using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>A publication attempt is fenced because its exact witness was unavailable at the target decision.</summary>
[Discriminator("bdgrz.criteria.witness-refused", 1)]
public sealed record CriterionWitnessRefused(Uuid EditionId, Uuid DraftGeneration, string Identifier,
    Uuid PublicationAttemptId, long AttemptOrdinal, Uuid IncarnationId, Uuid PublicationStartedEventId) : DomainEvent;

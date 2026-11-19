using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>One criterion incarnation is reserved as an edition's publication witness.</summary>
[Discriminator("bdgrz.criteria.witness-pinned", 1)]
public sealed record CriterionWitnessPinned(Uuid EditionId, Uuid DraftGeneration, string Identifier,
    Uuid PublicationAttemptId, long AttemptOrdinal, Uuid IncarnationId, Uuid PublicationStartedEventId) : DomainEvent;

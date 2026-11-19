using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>A specific request was refused while a different exact claim held this label.</summary>
[Discriminator("bdgrz.criteria.edition-label-reservation-refused", 1)]
public sealed record EditionLabelReservationRefused(string Label, Uuid EditionId, Uuid OperationId,
    long RequestedGeneration, Uuid EditionRequestEventId, ActorAttribution RequestedBy,
    Uuid HolderEditionId, Uuid HolderOperationId, long HolderGeneration, Uuid HolderReservationEventId) : DomainEvent;

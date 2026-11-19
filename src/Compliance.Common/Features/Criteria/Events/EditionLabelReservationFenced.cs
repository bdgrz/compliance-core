using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>Fences one cancelled or refused label request against a delayed reservation.</summary>
[Discriminator("bdgrz.criteria.edition-label-reservation-fenced", 1)]
public sealed record EditionLabelReservationFenced(string Label, Uuid EditionId, Uuid OperationId,
    Uuid EditionRequestEventId, Uuid EditionTerminalEventId, string Reason) : DomainEvent;

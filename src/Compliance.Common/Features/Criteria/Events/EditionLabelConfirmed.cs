using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>A reserved label was confirmed against its edition's accepted change.</summary>
[Discriminator("bdgrz.criteria.edition-label-confirmed", 1)]
public sealed record EditionLabelConfirmed(string Label, Uuid EditionId, Uuid OperationId, long Generation,
    Uuid EditionAcceptedEventId) : DomainEvent;

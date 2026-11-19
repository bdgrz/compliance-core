using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>One normalized edition label is reserved for an exact edition operation.</summary>
[Discriminator("bdgrz.criteria.edition-label-reserved", 1)]
public sealed record EditionLabelReserved(string Label, Uuid EditionId, Uuid OperationId, long Generation,
    Uuid EditionRequestEventId, ActorAttribution RequestedBy) : DomainEvent;

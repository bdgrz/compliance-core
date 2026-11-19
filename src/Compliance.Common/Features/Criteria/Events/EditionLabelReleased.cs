using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>An exact label reservation was released after its edition transition.</summary>
[Discriminator("bdgrz.criteria.edition-label-released", 1)]
public sealed record EditionLabelReleased(string Label, Uuid EditionId, Uuid OperationId, long Generation,
    Uuid EditionTerminalEventId, string Reason, Uuid ReplacementConfirmationEventId) : DomainEvent;

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Reconciles one edition activation or rename against its exact label reservation.</summary>
[Discriminator("bdgrz.criteria.confirm-edition-label", 1)]
sealed record ConfirmCriteriaEditionLabel(
    Uuid EditionId,
    Uuid OperationId,
    string Label,
    long Generation,
    Uuid ReservationEventId,
    ulong EditionEventPosition,
    Uuid EditionEventId) : IRequest;

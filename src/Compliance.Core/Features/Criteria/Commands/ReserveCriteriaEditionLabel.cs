using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Reconciles one committed edition label request with its label owner.</summary>
[Discriminator("bdgrz.criteria.reserve-edition-label", 1)]
sealed record ReserveCriteriaEditionLabel(Uuid EditionId, Uuid OperationId, string Label,
    ulong EditionEventPosition, Uuid EditionEventId) : IRequest;

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Delivers one committed label reservation to its edition owner.</summary>
[Discriminator("bdgrz.criteria.apply-label-reservation", 1)]
sealed record ApplyCriteriaLabelReservation(Uuid EditionId, Uuid OperationId, string Label,
    ulong SourcePosition, Uuid SourceEventId) : IRequest;

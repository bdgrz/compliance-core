using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Reconciles one exact claim after a committed edition transition.</summary>
[Discriminator("bdgrz.criteria.release-edition-label", 1)]
sealed record ReleaseCriteriaEditionLabel(Uuid EditionId, Uuid ClaimOperationId, string Label,
    long Generation, Uuid ReservationEventId, ulong EditionEventPosition, Uuid EditionEventId) : IRequest;

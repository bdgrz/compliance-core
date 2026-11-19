using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>A released seat became free after its exact target unmark was verified.</summary>
[Discriminator("bdgrz.platform.primary-operator.seat.vacated", 1)]
public sealed record PrimaryPlatformOperatorSeatVacated(Uuid OperationId, Uuid UserId, Uuid GrantEpisodeId,
    Uuid LineageId, ulong Generation, Uuid ReservationEventId, Uuid ReleaseEventId, Uuid TargetEventId) : DomainEvent;

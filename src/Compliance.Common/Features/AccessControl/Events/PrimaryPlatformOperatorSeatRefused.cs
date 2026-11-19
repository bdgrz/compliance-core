using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>An exact pending seat was closed after its target fenced the primary mark.</summary>
[Discriminator("bdgrz.platform.primary-operator.seat.refused", 1)]
public sealed record PrimaryPlatformOperatorSeatRefused(Uuid OperationId, Uuid UserId, Uuid GrantEpisodeId,
    Uuid LineageId, ulong Generation, Uuid ReservationEventId, Uuid TargetEventId) : DomainEvent;

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>An exact reserved primary was confirmed from its target's committed mark.</summary>
[Discriminator("bdgrz.platform.primary-operator.appointed", 1)]
public sealed record PrimaryPlatformOperatorAppointed(Uuid OperationId, Uuid UserId, Uuid GrantEpisodeId,
    Uuid LineageId, ulong Generation, Uuid ReservationEventId, Uuid TargetEventId) : DomainEvent;

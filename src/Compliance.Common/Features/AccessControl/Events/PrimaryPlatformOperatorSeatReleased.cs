using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>A confirmed primary seat entered release after another primary was confirmed.</summary>
[Discriminator("bdgrz.platform.primary-operator.seat.released", 1)]
public sealed record PrimaryPlatformOperatorSeatReleased(Uuid OperationId, ulong ExpectedRevision,
    Uuid UserId, Uuid GrantEpisodeId, Uuid LineageId, ulong Generation, Uuid ReservationEventId,
    Uuid ReplacementUserId, ulong ReplacementGeneration, Uuid ActorUserId, ActorAttribution Actor, string Reason) : DomainEvent;

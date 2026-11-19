using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>A reserved primary-mark generation was durably fenced at its target.</summary>
[Discriminator("bdgrz.platform.operator.primary-mark.fenced", 1)]
public sealed record PlatformOperatorPrimaryMarkFenced(Uuid UserId, Uuid OperationId, ulong ExpectedRevision,
    Uuid GrantEpisodeId, Uuid LineageId, ulong Generation, Uuid ReservationEventId) : DomainEvent;

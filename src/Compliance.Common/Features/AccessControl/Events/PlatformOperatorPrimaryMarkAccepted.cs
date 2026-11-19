using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>One active operator grant accepted an exact reserved primary mark.</summary>
[Discriminator("bdgrz.platform.operator.primary-mark.accepted", 1)]
public sealed record PlatformOperatorPrimaryMarkAccepted(Uuid UserId, Uuid OperationId, ulong ExpectedRevision,
    Uuid GrantEpisodeId, Uuid LineageId, ulong Generation, Uuid ReservationEventId) : DomainEvent;

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>An exact released primary mark was removed without revoking its operator grant.</summary>
[Discriminator("bdgrz.platform.operator.primary.unmarked", 1)]
public sealed record PlatformOperatorPrimaryUnmarked(Uuid UserId, Uuid OperationId, ulong ExpectedRevision,
    Uuid GrantEpisodeId, Uuid LineageId, ulong Generation, Uuid ReservationEventId, Uuid ReleaseEventId) : DomainEvent;

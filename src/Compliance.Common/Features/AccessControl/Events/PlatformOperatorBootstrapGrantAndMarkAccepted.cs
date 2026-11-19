using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>One bootstrap target atomically accepted its initial grant and reserved primary mark.</summary>
[Discriminator("bdgrz.platform.operator.bootstrap-grant-and-mark.accepted", 1)]
public sealed record PlatformOperatorBootstrapGrantAndMarkAccepted(Uuid UserId, Uuid OperationId,
    ulong ExpectedRevision, Uuid GrantEpisodeId, Uuid LineageId, ulong Generation, Uuid ReservationEventId,
    ActorAttribution Actor) : DomainEvent;

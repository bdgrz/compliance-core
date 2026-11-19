using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>One exact platform-operator grant episode became inactive.</summary>
[Discriminator("bdgrz.platform.operator.access.revoked", 1)]
public sealed record PlatformOperatorAccessRevoked(Uuid UserId, Uuid OperationId, ulong ExpectedRevision,
    Uuid GrantEpisodeId, Uuid ActorUserId, ActorAttribution Actor, string Reason) : DomainEvent;

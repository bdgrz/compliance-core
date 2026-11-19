using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>One user's platform-operator access became active.</summary>
[Discriminator("bdgrz.platform.operator.access.granted", 1)]
public sealed record PlatformOperatorAccessGranted(Uuid UserId, Uuid OperationId, ulong ExpectedRevision,
    Uuid ActorUserId, ActorAttribution Actor, string Reason) : DomainEvent;

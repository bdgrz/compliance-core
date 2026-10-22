using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.platform.operator.granted", 1)]
public sealed record PlatformOperatorGranted(Uuid CommandId, ulong ExpectedRevision,
    Uuid ActorUserId, Uuid SubjectUserId, ActorAttribution Actor, DateTimeOffset OccurredAt,
    string Reason, string? ActorDisplay = null) : DomainEvent;

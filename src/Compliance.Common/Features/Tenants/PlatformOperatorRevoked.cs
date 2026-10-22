using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.platform.operator.revoked", 1)]
public sealed record PlatformOperatorRevoked(Uuid CommandId, ulong ExpectedRevision,
    Uuid ActorUserId, Uuid SubjectUserId, Uuid AssignmentId, ActorAttribution Actor, DateTimeOffset OccurredAt,
    string Reason, string? ActorDisplay = null) : DomainEvent;

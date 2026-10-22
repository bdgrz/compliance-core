using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.platform.operator-roster.seeded", 1)]
public sealed record PlatformOperatorRosterSeeded(Uuid RosterId, Uuid[] UserIds,
    ActorAttribution Actor, DateTimeOffset OccurredAt) : DomainEvent;

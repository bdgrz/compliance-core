using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.access-grant.revoked", 1)]
public sealed record AccessGrantRevoked(
    Uuid TenantId,
    Uuid GrantId,
    ActorReference RevokedBy,
    DateTimeOffset RevokedAt) : DomainEvent;

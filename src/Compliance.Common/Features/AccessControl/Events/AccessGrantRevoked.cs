using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.access-grant.revoked", 1)]
public sealed record AccessGrantRevoked(
    Uuid TenantId,
    Uuid GrantId,
    ActorReference RevokedBy,
    DateTimeOffset RevokedAt) : DomainEvent;

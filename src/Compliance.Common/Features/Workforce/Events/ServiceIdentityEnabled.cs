using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.service-identity.enabled", 1)]
public sealed record ServiceIdentityEnabled(Uuid TenantId, Uuid ServiceIdentityId, long Revision,
    ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

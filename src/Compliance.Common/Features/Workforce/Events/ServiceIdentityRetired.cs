using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.service-identity.retired", 1)]
public sealed record ServiceIdentityRetired(Uuid TenantId, Uuid ServiceIdentityId, long Revision,
    string? Reason, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

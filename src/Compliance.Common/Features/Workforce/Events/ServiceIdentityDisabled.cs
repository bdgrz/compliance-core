using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.service-identity.disabled", 1)]
public sealed record ServiceIdentityDisabled(Uuid TenantId, Uuid ServiceIdentityId, long Revision,
    string? Reason, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.service-identity.purpose.changed", 1)]
public sealed record ServiceIdentityPurposeChanged(Uuid TenantId, Uuid ServiceIdentityId, long Revision,
    string Purpose, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

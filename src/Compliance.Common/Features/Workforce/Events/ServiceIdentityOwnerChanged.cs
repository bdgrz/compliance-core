using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.service-identity.owner.changed", 1)]
public sealed record ServiceIdentityOwnerChanged(Uuid TenantId, Uuid ServiceIdentityId, long Revision,
    string PreviousOwnerKind, Uuid PreviousOwnerId, string OwnerKind, Uuid OwnerId, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

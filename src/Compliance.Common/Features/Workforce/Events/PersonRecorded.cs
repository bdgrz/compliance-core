using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.person.recorded", 1)]
public sealed record PersonRecorded(Uuid TenantId, Uuid PersonId, string DisplayName,
    string? WorkEmail, ActorReference Actor, DateTimeOffset ChangedAt,
    PersonalContactDetails? PersonalContact = null) : DomainEvent;

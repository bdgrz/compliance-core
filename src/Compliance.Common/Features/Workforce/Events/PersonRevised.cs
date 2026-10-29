using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.person.revised", 1)]
public sealed record PersonRevised(Uuid TenantId, Uuid PersonId, long Revision,
    string DisplayName, string? WorkEmail, ActorReference Actor, DateTimeOffset ChangedAt,
    PersonalContactDetails? PersonalContact = null)
    : DomainEvent;

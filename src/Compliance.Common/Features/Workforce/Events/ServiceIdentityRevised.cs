using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.service-identity.revised", 1)]
public sealed record ServiceIdentityRevised(Uuid TenantId, Uuid ServiceIdentityId, long Revision,
    ServiceIdentityTerms Terms, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

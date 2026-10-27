using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.access-grant.issued", 1)]
public sealed record AccessGrantIssued(Uuid TenantId, Uuid GrantId, AccessGrantTerms Terms,
    Uuid? MembershipEpisodeId = null) : DomainEvent;

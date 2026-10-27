using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.work-relationship.revised", 1)]
public sealed record WorkRelationshipRevised(Uuid TenantId, Uuid RelationshipId, long Revision,
    WorkRelationshipTerms Terms, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

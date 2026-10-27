using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.work-relationship.recorded", 1)]
public sealed record WorkRelationshipRecorded(Uuid TenantId, Uuid RelationshipId, Uuid PersonId,
    string SourceWorkerId, WorkRelationshipTerms Terms, ActorReference Actor,
    DateTimeOffset ChangedAt) : DomainEvent;

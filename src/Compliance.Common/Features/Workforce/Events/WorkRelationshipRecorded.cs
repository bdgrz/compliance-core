using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.work-relationship.recorded", 1)]
public sealed record WorkRelationshipRecorded(Uuid TenantId, Uuid RelationshipId, Uuid PersonId,
    string SourceWorkerId, WorkRelationshipTerms Terms, ActorReference Actor,
    DateTimeOffset ChangedAt) : DomainEvent;

using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.work-relationship.manager-changed", 1)]
public sealed record WorkRelationshipManagerChanged(Uuid TenantId, Uuid RelationshipId, string WorkerIdentifier,
    long Revision, Uuid? ManagerPersonId, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

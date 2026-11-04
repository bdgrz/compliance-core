using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.work-relationship.started", 1)]
public sealed record WorkRelationshipStarted(Uuid TenantId, Uuid RelationshipId, string WorkerIdentifier,
    long Revision, DateOnly StartDate, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.work-relationship.leave-started", 1)]
public sealed record WorkRelationshipLeaveStarted(Uuid TenantId, Uuid RelationshipId, string WorkerIdentifier,
    long Revision, DateOnly FromDate, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

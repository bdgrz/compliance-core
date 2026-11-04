using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.work-relationship.leave-ended", 1)]
public sealed record WorkRelationshipLeaveEnded(Uuid TenantId, Uuid RelationshipId, string WorkerIdentifier,
    long Revision, DateOnly ReturnDate, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

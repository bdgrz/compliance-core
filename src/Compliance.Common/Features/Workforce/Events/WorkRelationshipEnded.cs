using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.work-relationship.ended", 1)]
public sealed record WorkRelationshipEnded(Uuid TenantId, Uuid RelationshipId, string WorkerIdentifier,
    long Revision, DateOnly EndDate, string? Reason, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

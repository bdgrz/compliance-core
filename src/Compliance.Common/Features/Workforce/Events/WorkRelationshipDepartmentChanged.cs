using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.work-relationship.department-changed", 1)]
public sealed record WorkRelationshipDepartmentChanged(Uuid TenantId, Uuid RelationshipId, string WorkerIdentifier,
    long Revision, string? Department, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

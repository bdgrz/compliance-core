using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.work-relationship.dates-corrected", 1)]
public sealed record WorkRelationshipDatesCorrected(Uuid TenantId, Uuid RelationshipId, string WorkerIdentifier,
    long Revision, DateOnly StartDate, DateOnly? EndDate, string Reason, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

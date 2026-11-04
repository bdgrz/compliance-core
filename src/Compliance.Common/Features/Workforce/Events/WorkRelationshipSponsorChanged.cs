using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.work-relationship.sponsor-changed", 1)]
public sealed record WorkRelationshipSponsorChanged(Uuid TenantId, Uuid RelationshipId, string WorkerIdentifier,
    long Revision, Uuid SponsorPersonId, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

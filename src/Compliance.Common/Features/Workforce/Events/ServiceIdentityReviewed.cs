using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.service-identity.reviewed", 1)]
public sealed record ServiceIdentityReviewed(Uuid TenantId, Uuid ServiceIdentityId, long Revision,
    ActorReference Reviewer, string? Notes, DateOnly NextReviewDate, DateTimeOffset ReviewedAt) : DomainEvent;

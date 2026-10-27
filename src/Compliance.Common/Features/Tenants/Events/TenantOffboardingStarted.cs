using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Events;

[Discriminator("bdgrz.tenant.offboarding.started", 1)]
public sealed record TenantOffboardingStarted(Uuid TenantId, Uuid OperatorUserId, string Reason,
    DateTimeOffset StartedAt) : DomainEvent;

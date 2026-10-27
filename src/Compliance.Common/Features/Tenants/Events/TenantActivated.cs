using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Events;

[Discriminator("bdgrz.tenant.activated", 1)]
public sealed record TenantActivated(Uuid TenantId, Uuid FirstAdministratorUserId) : DomainEvent;

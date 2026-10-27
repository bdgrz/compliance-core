using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Events;

[Discriminator("bdgrz.tenant.registered", 1)]
public sealed record TenantRegistered(Uuid TenantId, Uuid OwnerUserId, string Name, string Slug,
    string? LegalName = null, string? FirstAdministratorEmail = null,
    bool CreatorIsAdministrator = false, bool ActivationRequired = false) : DomainEvent;

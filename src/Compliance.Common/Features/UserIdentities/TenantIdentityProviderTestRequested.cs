using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Records a tenant administrator's request to test the current provider credentials.</summary>
[Discriminator("bdgrz.user-identities.tenant-identity-provider-test-requested", 1)]
public sealed record TenantIdentityProviderTestRequested(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    TenantIdentityProviderTestRequestView Request,
    TenantIdentityProviderView State,
    DateTimeOffset OccurredAt) : DomainEvent;

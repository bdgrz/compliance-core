using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Retains tenant-scoped provider configuration and secret reference for replay.</summary>
[Discriminator("bdgrz.useridentities.tenant-identity-provider-configured", 2)]
public sealed record TenantIdentityProviderConfigured(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TenantIdentityProviderConfigurationContent Configuration,
    TenantIdentityProviderView State,
    DateTimeOffset OccurredAt) : DomainEvent;

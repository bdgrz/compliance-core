using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Retains credential reference rotation and forces a fresh connection test.</summary>
[Discriminator("bdgrz.useridentities.tenant-identity-provider-credentials-rotated", 2)]
public sealed record TenantIdentityProviderCredentialsRotated(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    string EncryptedCredentialReference,
    string RotationReference,
    TenantIdentityProviderView State,
    DateTimeOffset OccurredAt) : DomainEvent;

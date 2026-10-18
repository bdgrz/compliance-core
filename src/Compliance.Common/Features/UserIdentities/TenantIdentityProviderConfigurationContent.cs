namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Public OIDC configuration; credential references are retained only in the event stream.</summary>
public sealed record TenantIdentityProviderConfigurationContent(
    string Issuer,
    string ClientId,
    string EncryptedCredentialReference,
    IReadOnlyList<string> AllowedAudiences);

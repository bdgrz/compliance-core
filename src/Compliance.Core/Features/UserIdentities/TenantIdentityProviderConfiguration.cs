namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     A validated, normalized OIDC connection definition. <paramref name="CredentialReference" /> is the secret-store
///     pointer; it is handled only to be frozen as protected content and to be compared by digest.
/// </summary>
public sealed record TenantIdentityProviderConfiguration(string Issuer, string ClientId,
    IReadOnlyList<string> AllowedAudiences, string CredentialReference);

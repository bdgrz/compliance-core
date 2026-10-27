namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     The protected content a connection's credential epoch points at: the secret-store pointer the provider test
///     worker resolves through the vault. Facts and intent digests carry the content reference and its digest, never
///     this value.
/// </summary>
public sealed record TenantIdentityProviderCredentialContent(string EncryptedCredentialReference)
{
    /// <summary>Schema of the protected content.</summary>
    public const string Schema = "tenant-identity-provider-credential.v1";
}

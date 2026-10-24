using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     The authored fields of a credential rotation that its intent digest covers. The secret-store pointer is
///     represented by the digest of its protected content, never by the pointer.
/// </summary>
public sealed record RotateTenantIdentityProviderCredentialsAuthoredIntent(Uuid TenantId, Uuid ProviderConnectionId,
    string CredentialSha256, string RotationReference, long ExpectedRevision);

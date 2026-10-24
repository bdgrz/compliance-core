using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     The authored fields of a connection configuration that its intent digest covers. The secret-store pointer is
///     represented by the digest of its protected content, never by the pointer, and the audiences are in their
///     normalized order so an equivalent retry compares equal.
/// </summary>
public sealed record ConfigureTenantIdentityProviderAuthoredIntent(Uuid TenantId, Uuid ProviderConnectionId,
    string Issuer, string ClientId, IReadOnlyList<string> AllowedAudiences, string CredentialSha256,
    long ExpectedRevision);

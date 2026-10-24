using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     The native acceptance of <see cref="RotateTenantIdentityProviderCredentials" />: a new credential epoch whose
///     secret-store pointer lives in protected content, and a forced fresh connection test.
/// </summary>
[Discriminator("bdgrz.useridentities.tenant-identity-provider-credentials-rotated", 2)]
public sealed record TenantIdentityProviderCredentialsRotated(
    Uuid TenantId,
    Uuid ProviderConnectionId,
    ImmutableContentRef CredentialReference,
    string RotationReference,
    long CredentialRevision,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset RotatedAt,
    OperationProvenance Operation,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

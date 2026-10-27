using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     The native acceptance of <see cref="ConfigureTenantIdentityProvider" />: public OIDC configuration and a
///     reference to the protected content that holds the secret-store pointer, never the pointer itself.
/// </summary>
[Discriminator("bdgrz.useridentities.tenant-identity-provider-configured", 2)]
public sealed record TenantIdentityProviderConfigured(
    Uuid TenantId,
    Uuid ProviderConnectionId,
    string Issuer,
    string ClientId,
    IReadOnlyList<string> AllowedAudiences,
    ImmutableContentRef CredentialReference,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset ConfiguredAt,
    OperationProvenance Operation,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

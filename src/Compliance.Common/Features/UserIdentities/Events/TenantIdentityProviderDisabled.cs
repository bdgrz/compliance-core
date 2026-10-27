using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>The native acceptance of <see cref="DisableTenantIdentityProvider" />; a disabled connection stays terminal.</summary>
[Discriminator("bdgrz.useridentities.tenant-identity-provider-disabled", 2)]
public sealed record TenantIdentityProviderDisabled(
    Uuid TenantId,
    Uuid ProviderConnectionId,
    string Rationale,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset DisabledAt,
    OperationProvenance Operation,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

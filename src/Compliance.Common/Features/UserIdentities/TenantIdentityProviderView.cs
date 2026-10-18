using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Safe tenant identity-provider state; credential and secret references are deliberately absent.</summary>
public sealed record TenantIdentityProviderView(
    Uuid TenantId,
    Uuid ProviderConnectionId,
    string Status,
    long Revision,
    long CredentialRevision,
    string Issuer,
    string ClientId,
    IReadOnlyList<string> AllowedAudiences,
    IReadOnlyList<TenantIdentityProviderTestView> TestHistory,
    string? LastRotationReference,
    string? DisabledRationale,
    Uuid ConfiguredByMemberId,
    string ConfiguredByDisplay,
    DateTimeOffset ConfiguredAt,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset UpdatedAt,
    string ContentSha256);

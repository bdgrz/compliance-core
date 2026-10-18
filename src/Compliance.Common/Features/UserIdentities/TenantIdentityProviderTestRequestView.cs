using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Durable request to test one exact provider credential revision.</summary>
public sealed record TenantIdentityProviderTestRequestView(
    string TestKey,
    long CredentialRevision,
    Uuid RequestedByMemberId,
    string RequestedByDisplay,
    DateTimeOffset RequestedAt);

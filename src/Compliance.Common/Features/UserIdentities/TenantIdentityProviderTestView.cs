using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Last bounded issuer/audience connectivity test with safe failure detail.</summary>
public sealed record TenantIdentityProviderTestView(
    string TestKey,
    long CredentialRevision,
    string Outcome,
    string? FailureCode,
    string? ActionableMessage,
    Uuid? TestedByMemberId,
    string TestedByDisplay,
    DateTimeOffset TestedAt,
    ActorReference? TestedByActor = null,
    Uuid? TestRequestEventId = null);

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Retains a bounded safe test result; provider tokens and response bodies are never persisted.</summary>
[Discriminator("bdgrz.useridentities.tenant-identity-provider-tested", 2)]
public sealed record TenantIdentityProviderTested(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TenantIdentityProviderTestResultContent Result,
    TenantIdentityProviderView State,
    DateTimeOffset OccurredAt) : DomainEvent;

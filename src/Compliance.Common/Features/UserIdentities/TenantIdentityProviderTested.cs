using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Retains a bounded safe test result; provider tokens and response bodies are never persisted.</summary>
[Discriminator("bdgrz.useridentities.tenant-identity-provider-tested", 3)]
public sealed record TenantIdentityProviderTested(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    Uuid TestRequestEventId,
    ActorReference TestedByActor,
    TenantIdentityProviderTestResultContent Result,
    TenantIdentityProviderView State,
    DateTimeOffset OccurredAt) : DomainEvent;

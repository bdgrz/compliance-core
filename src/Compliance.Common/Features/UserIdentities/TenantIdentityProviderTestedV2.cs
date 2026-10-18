using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Legacy provider-test event retained for replay of discriminator version 2.</summary>
[Discriminator("bdgrz.useridentities.tenant-identity-provider-tested", 2)]
public sealed record TenantIdentityProviderTestedV2(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TenantIdentityProviderTestResultContent Result,
    TenantIdentityProviderView State,
    DateTimeOffset OccurredAt) : DomainEvent;

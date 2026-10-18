using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Disables one connection without changing any platform-user identity or membership history.</summary>
[Discriminator("bdgrz.useridentities.tenant-identity-provider-disabled", 2)]
public sealed record TenantIdentityProviderDisabled(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    string Rationale,
    TenantIdentityProviderView State,
    DateTimeOffset OccurredAt) : DomainEvent;

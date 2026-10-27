using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Audited provider display text admitted at the proven identity revision; never identity or authority.</summary>
[Discriminator("bdgrz.user-identity.profile-observed", 2)]
public sealed record UserIdentityProfileObserved(Uuid UserId, string? DisplayName,
    DateTimeOffset ObservedAt, IdentityRegistrationProof Source) : DomainEvent;

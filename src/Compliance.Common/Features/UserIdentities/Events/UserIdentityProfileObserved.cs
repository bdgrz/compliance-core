using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Events;

/// <summary>Audited provider display text admitted at the proven identity revision; never identity or authority.</summary>
[Discriminator("bdgrz.user-identity.profile-observed", 2)]
public sealed record UserIdentityProfileObserved(Uuid UserId, string? DisplayName,
    DateTimeOffset ObservedAt, IdentityRegistrationProof Source) : DomainEvent;

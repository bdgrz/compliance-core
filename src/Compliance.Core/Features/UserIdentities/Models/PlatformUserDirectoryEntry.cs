using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.UserIdentities.Models;

public sealed record PlatformUserDirectoryEntry(Uuid UserId, string? DisplayName = null,
    DateTimeOffset? ProfileObservedAt = null, Uuid? ProfileObservationId = null);

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Models;

/// <summary>An active platform operator as of the directory projection checkpoint.</summary>
public sealed record PlatformOperatorDirectoryEntry(Uuid UserId, Uuid GrantEpisodeId, bool IsPrimary);

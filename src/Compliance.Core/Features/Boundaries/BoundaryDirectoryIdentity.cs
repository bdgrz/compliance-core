using Bdgrz.Compliance.Features.Boundaries.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

/// <summary>Compact current or immutable historical source identity; content stays in its native owner history.</summary>
public sealed record BoundaryDirectoryIdentity(BoundaryAuthorityHeader Header, long NativeSequence,
    Uuid? VersionId = null, DateOnly? EffectiveFrom = null, Uuid? DecisionId = null, DateTimeOffset? DecidedAt = null);

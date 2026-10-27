using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Models;

public sealed record ArtifactLegalHoldView(Uuid HoldId, string Reason,
    Bdgrz.Compliance.Features.AccessControl.Models.ActorReference PlacedBy, DateTimeOffset PlacedAt,
    string? ReleaseReason = null, Bdgrz.Compliance.Features.AccessControl.Models.ActorReference? ReleasedBy = null,
    DateTimeOffset? ReleasedAt = null);

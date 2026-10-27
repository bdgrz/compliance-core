using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Models;

/// <summary>Engagement hold scope, report-based expiry, and attributable release history.</summary>
public sealed record ArtifactEngagementHoldView(
    Uuid TenantId,
    Uuid HoldId,
    Uuid EngagementId,
    Uuid? ReportArtifactId,
    string Scope,
    string Authority,
    string Reason,
    string Status,
    long Revision,
    ActorReference PlacedBy,
    DateTimeOffset PlacedAt,
    string? IssuerReference,
    ActorReference? ReportIssuedBy,
    DateTimeOffset? ReportIssuedAt,
    DateOnly? ReleaseEligibleOn,
    ActorReference? ReleasedBy,
    string? ReleaseAuthority,
    string? ReleaseRationale,
    DateTimeOffset? ReleasedAt,
    DateTimeOffset UpdatedAt);

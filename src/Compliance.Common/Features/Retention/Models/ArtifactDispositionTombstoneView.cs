using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Retention.Models;

/// <summary>The retained non-content proof left after governed content removal.</summary>
public sealed record ArtifactDispositionTombstoneView(
    Uuid ArtifactId,
    string ContentSha256,
    long ContentLength,
    string HandlingClass,
    string RetentionPolicy,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly RetainsThrough,
    string ApprovalRationale,
    ActorReference ApprovedBy,
    DateTimeOffset ApprovedAt,
    ActorReference DisposedBy,
    DateTimeOffset DisposedAt);

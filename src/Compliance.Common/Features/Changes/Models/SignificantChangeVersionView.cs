using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes.Models;

/// <summary>An immutable significant-change content version and its recorder attribution.</summary>
public sealed record SignificantChangeVersionView(
    int Version,
    SignificantChangeContent Content,
    string ContentSha256,
    Uuid RecordedByMemberId,
    string RecordedByDisplay,
    DateTimeOffset RecordedAt,
    string ChangeReason);

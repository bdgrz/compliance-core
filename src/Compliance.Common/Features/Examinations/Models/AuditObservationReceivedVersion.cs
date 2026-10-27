using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>One immutable externally authored version; the recording member is not its external author.</summary>
public sealed record AuditObservationReceivedVersion(int Version, Uuid RequestId,
    AuditObservationContent Content, string ContentSha256,
    Uuid RecordedByMemberId, string RecordedByDisplay, DateTimeOffset RecordedAt);

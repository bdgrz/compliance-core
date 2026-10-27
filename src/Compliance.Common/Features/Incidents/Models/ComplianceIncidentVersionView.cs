using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>An immutable incident content version and its recorder attribution.</summary>
public sealed record ComplianceIncidentVersionView(
    int Version,
    ComplianceIncidentContent Content,
    string ContentSha256,
    Uuid RecordedByMemberId,
    string RecordedByDisplay,
    DateTimeOffset RecordedAt,
    string ChangeReason);

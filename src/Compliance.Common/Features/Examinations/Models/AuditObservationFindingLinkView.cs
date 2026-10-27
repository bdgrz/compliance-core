using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>The latest attributable finding link; remediation and closure remain owned by the finding.</summary>
public sealed record AuditObservationFindingLinkView(Uuid RequestId, int SourceVersion,
    AuditObservationFindingReference Finding, Uuid RecordedByMemberId,
    string RecordedByDisplay, DateTimeOffset RecordedAt);

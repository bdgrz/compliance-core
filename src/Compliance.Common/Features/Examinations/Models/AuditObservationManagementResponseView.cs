using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>The latest management response, explicitly bound to the received source version it answered.</summary>
public sealed record AuditObservationManagementResponseView(Uuid RequestId, int SourceVersion,
    string Text, Uuid RecordedByMemberId, string RecordedByDisplay, DateTimeOffset RecordedAt);

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>An immutable incident resolution with exact evidence and completed follow-up work.</summary>
public sealed record ComplianceIncidentResolutionView(
    int ContentVersion,
    string Resolution,
    DateTimeOffset ResolvedAt,
    IReadOnlyList<Uuid> EvidenceArtifactIds,
    IReadOnlyList<Uuid> ResolvedFollowUpWorkIds,
    Uuid ResolvedByMemberId,
    string ResolvedByDisplay);

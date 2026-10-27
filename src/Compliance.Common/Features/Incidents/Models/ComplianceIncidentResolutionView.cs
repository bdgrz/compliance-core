using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>An immutable incident resolution with exact evidence and proven completed follow-up work.</summary>
public sealed record ComplianceIncidentResolutionView(
    int ContentVersion,
    string Resolution,
    DateTimeOffset ResolvedAt,
    IReadOnlyList<Uuid> EvidenceArtifactIds,
    IReadOnlyList<Uuid> ResolvedFollowUpWorkIds,
    IReadOnlyList<ComplianceIncidentFollowUpCompletionView> FollowUpCompletions,
    Uuid ResolvedByMemberId,
    string ResolvedByDisplay);

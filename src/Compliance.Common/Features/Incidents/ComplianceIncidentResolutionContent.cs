using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Resolution rationale, exact evidence, and completed follow-up work for an incident.</summary>
public sealed record ComplianceIncidentResolutionContent(
    string Resolution,
    DateTimeOffset ResolvedAt,
    IReadOnlyList<Uuid> EvidenceArtifactIds,
    IReadOnlyList<Uuid> ResolvedFollowUpWorkIds);

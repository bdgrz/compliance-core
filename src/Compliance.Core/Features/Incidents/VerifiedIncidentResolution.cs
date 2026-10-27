using Bdgrz.Compliance.Features.Incidents.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>
///     A resolution whose follow-up completions were verified against their committed native facts. The incident
///     never accepts a work item as complete from a caller-supplied identifier.
/// </summary>
/// <param name="Resolution">The resolution rationale.</param>
/// <param name="ResolvedAt">When the incident was resolved.</param>
/// <param name="EvidenceArtifactIds">The governed evidence artifacts.</param>
/// <param name="Completions">One verified completion per assessed follow-up work item.</param>
public sealed record VerifiedIncidentResolution(string Resolution, DateTimeOffset ResolvedAt,
    IReadOnlyList<Uuid> EvidenceArtifactIds, IReadOnlyList<ComplianceIncidentFollowUpCompletionView> Completions);

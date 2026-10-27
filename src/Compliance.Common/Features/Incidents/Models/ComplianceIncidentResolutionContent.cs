using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>
///     Resolution rationale, exact evidence, and the native facts that complete every assessed follow-up work item.
///     Each fact must be a committed corrective-action completion; the incident verifies it against its stream.
/// </summary>
public sealed record ComplianceIncidentResolutionContent(
    string Resolution,
    DateTimeOffset ResolvedAt,
    IReadOnlyList<Uuid> EvidenceArtifactIds,
    IReadOnlyList<NativeFactReference> FollowUpCompletionFacts);

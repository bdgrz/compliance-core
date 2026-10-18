using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>A compliance-owned incident fact, with optional external or earlier risk provenance.</summary>
public sealed record ComplianceIncidentContent(
    string? SourceSystem,
    string? SourceIdentifier,
    string? SourceUrl,
    RiskIncidentReference? EarlierRiskIncidentReference,
    string Title,
    string Description,
    string IncidentKind,
    string Severity,
    DateTimeOffset OccurredAt,
    DateTimeOffset DetectedAt,
    Uuid OwnerMemberId,
    bool CustomerDataAffected,
    bool AvailabilitySlaAffected,
    IReadOnlyList<Uuid> AffectedSystemIds,
    string HandlingClass,
    IReadOnlyList<Uuid> EvidenceArtifactIds);

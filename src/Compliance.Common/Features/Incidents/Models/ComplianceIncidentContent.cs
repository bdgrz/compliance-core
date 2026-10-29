using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Incidents.Commands;
namespace Bdgrz.Compliance.Features.Incidents.Models;

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

using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>A manually supplied or imported population row before attribution.</summary>
public sealed record AuditPopulationRowContent(
    Uuid RowId,
    string? SourceRowIdentity,
    string SourceReference,
    string Classification,
    string ClassificationRationale,
    IReadOnlyDictionary<string, string> AuthorizedFields,
    IReadOnlyList<AuditPopulationRecordReference> SourceRecordReferences,
    IReadOnlyList<Uuid> EvidenceArtifactIds);

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>Stable, attributable row identity and exact source and domain references.</summary>
public sealed record AuditPopulationRowView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid AuditPopulationId,
    Uuid RowId,
    Uuid SourceSnapshotId,
    string? SourceRowIdentity,
    string SourceReference,
    string Classification,
    string ClassificationRationale,
    IReadOnlyDictionary<string, string> AuthorizedFields,
    IReadOnlyList<AuditPopulationRecordReference> SourceRecordReferences,
    IReadOnlyList<Uuid> EvidenceArtifactIds,
    Uuid RecordedByMemberId,
    string RecordedByDisplay,
    DateTimeOffset RecordedAt);

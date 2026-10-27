using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Stable export identity pinned to an immutable population version.</summary>
public sealed record AuditPopulationExportView(
    Uuid ExportId,
    int PopulationVersion,
    string Format,
    string FileName,
    string PopulationContentSha256,
    Uuid CloseSnapshotId,
    DateTimeOffset GeneratedAt,
    Uuid GeneratedByMemberId,
    string GeneratedByDisplay,
    Uuid ArtifactId = default,
    string? OutputContentSha256 = null,
    long? OutputContentLength = null);

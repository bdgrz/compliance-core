using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports.Models;

/// <summary>A reconciled export source record or a withheld record with its reason.</summary>
public sealed record ClientExportRecordContent(
    string RecordType,
    Uuid RecordId,
    int Version,
    string ContentSha256,
    string Disposition,
    string? WithheldReasonCode,
    string? WithheldReason,
    IReadOnlyList<Uuid> ArtifactIds);

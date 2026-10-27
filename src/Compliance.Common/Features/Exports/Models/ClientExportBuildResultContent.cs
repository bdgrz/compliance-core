using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports;

/// <summary>Retained builder result for one deterministic, retryable export attempt.</summary>
public sealed record ClientExportBuildResultContent(
    Uuid AttemptId,
    int AttemptNumber,
    string Outcome,
    Uuid? ArchiveArtifactId,
    string? ArchiveSha256,
    string? FailureCode,
    Uuid? ManifestId,
    int IncludedRecordCount,
    int WithheldRecordCount,
    Uuid SourceSnapshotId,
    string SourceSnapshotSha256);

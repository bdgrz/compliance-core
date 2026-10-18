using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports;

/// <summary>An attributed export build attempt and its exact source reconciliation.</summary>
public sealed record ClientExportBuildAttemptView(
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
    string SourceSnapshotSha256,
    Uuid CompletedByMemberId,
    string CompletedByDisplay,
    DateTimeOffset CompletedAt);

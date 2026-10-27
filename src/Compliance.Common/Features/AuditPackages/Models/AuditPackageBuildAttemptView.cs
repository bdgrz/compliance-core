using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>Progress and exact output identity for one retryable package build.</summary>
public sealed record AuditPackageBuildAttemptView(
    Uuid BuildAttemptId,
    string BuildKey,
    string Status,
    int CompletedEntryCount,
    int TotalEntryCount,
    DateTimeOffset RequestedAt,
    DateTimeOffset UpdatedAt,
    Uuid? RequestedByMemberId,
    string RequestedByDisplay,
    DateTimeOffset? CompletedAt,
    string? FailureCode,
    string? ResultFingerprint,
    Uuid? ArchiveArtifactId,
    string? ArchiveSha256,
    long? ArchiveBytes);

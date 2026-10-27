using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>Worker output for a successful build or a bounded, retryable failure.</summary>
public sealed record AuditPackageBuildOutput(
    Uuid? ManifestId,
    string? ManifestSha256,
    IReadOnlyList<AuditPackageManifestEntryView>? IncludedEntries,
    Uuid? ArchiveArtifactId,
    string? ArchiveSha256,
    long? ArchiveBytes,
    string? FailureCode);

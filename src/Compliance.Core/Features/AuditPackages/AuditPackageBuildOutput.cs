using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Worker-reported output used to prove complete manifest coverage before success.</summary>
public sealed record AuditPackageBuildOutput(
    Uuid ManifestId,
    string ManifestSha256,
    IReadOnlyList<AuditPackageManifestEntryView> IncludedEntries,
    Uuid ArchiveArtifactId,
    string ArchiveSha256,
    long ArchiveBytes,
    string? FailureCode);

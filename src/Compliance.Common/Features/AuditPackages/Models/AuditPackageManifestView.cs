using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>Deterministic manifest binding every delivered path to exact source content.</summary>
public sealed record AuditPackageManifestView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid AuditPackageManifestId,
    Uuid AuditPackageId,
    int PackageVersion,
    Uuid SourceSnapshotId,
    string SourceSnapshotSha256,
    IReadOnlyList<Uuid> IncludedRecordIds,
    IReadOnlyList<AuditPackageManifestEntryView> Entries,
    string ManifestSha256,
    DateTimeOffset CreatedAt);

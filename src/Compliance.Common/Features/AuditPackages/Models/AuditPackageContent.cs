using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>Frozen source scope and explicit exact-version package selection supplied by a handler.</summary>
public sealed record AuditPackageContent(
    Uuid ExaminationId,
    string PackageKind,
    string SourceSnapshotKind,
    Uuid SourceSnapshotId,
    string SourceSnapshotSha256,
    string Name,
    IReadOnlyList<AuditPackageSelectionEntry> SelectedContent,
    Uuid SourcePeriodId = default);

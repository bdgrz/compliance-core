namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Verified immutable build admission and exact manifest/selection, independent of current source owners.</summary>
public sealed record VerifiedPackageBuild(AuditPackageManifest Owner, AuditPackageBuildRequested Admission,
    PackageBuildIntent Intent, AuditPackageManifestView Manifest, AuditPackageManifestHistorySnapshot History);

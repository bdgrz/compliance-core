using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Models;
namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>The native manifest and its composed read view; owner revisions never stand in for child revisions.</summary>
public sealed record AuditPackageSnapshot(AuditPackageManifest Owner, AuditPackageView? View,
    AuditPackageManifestHistorySnapshot History);

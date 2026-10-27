using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports.Models;

/// <summary>An exact M0-D27 collaboration package required for an attest export.</summary>
public sealed record ClientExportAttestPackageContent(
    Uuid EngagementId,
    Uuid PackageId,
    string ManifestSha256,
    int ItemCount);

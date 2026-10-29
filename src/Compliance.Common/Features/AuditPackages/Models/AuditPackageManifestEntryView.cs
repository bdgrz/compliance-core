namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>A manifest path bound to one exact selected source record version.</summary>
public sealed record AuditPackageManifestEntryView(
    AuditPackageSelectionEntry Source,
    string RelativePath);

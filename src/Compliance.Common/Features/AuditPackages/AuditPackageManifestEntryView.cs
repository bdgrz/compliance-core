namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>A manifest path bound to one exact selected source record version.</summary>
public sealed record AuditPackageManifestEntryView(
    AuditPackageSelectionEntry Source,
    string RelativePath);

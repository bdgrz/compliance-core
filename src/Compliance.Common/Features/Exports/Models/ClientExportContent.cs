using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Exports.Models;

/// <summary>A frozen, tenant-scoped inventory of exportable and withheld records.</summary>
public sealed record ClientExportContent(
    string Scope,
    string Format,
    string ExportKey,
    Uuid SourceSnapshotId,
    string SourceSnapshotSha256,
    int ExpectedRecordCount,
    IReadOnlyList<ClientExportRecordContent> Records,
    IReadOnlyList<ClientExportAttestPackageContent> RequiredAttestPackages);

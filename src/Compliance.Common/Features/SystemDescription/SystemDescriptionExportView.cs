using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>A report-ready export identity tied to one approved description version.</summary>
public sealed record SystemDescriptionExportView(Uuid ExportId, Uuid VersionId, long Version,
    string Format, string ExportKey, string ContentSha256, Uuid GeneratedByMemberId,
    string GeneratedByDisplay, DateTimeOffset GeneratedAt);

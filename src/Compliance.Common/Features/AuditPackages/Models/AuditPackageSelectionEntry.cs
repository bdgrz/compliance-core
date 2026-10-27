using Bdgrz.Compliance.Features.Snapshots.Models;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>An explicitly selected, immutable source version and its authored content identity.</summary>
public sealed record AuditPackageSelectionEntry(
    string Section,
    string StableId,
    string SourceType,
    Uuid SourceRecordId,
    long SourceVersion,
    string Extension,
    string ContentSha256,
    long ByteLength,
    string Authorship,
    Uuid SourceVersionId = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SnapshotSourceBlob? SourceContent = null);

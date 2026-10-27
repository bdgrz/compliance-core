using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots.Models;

/// <summary>One exact logical source version and the independently verified physical bytes representing it.</summary>
public sealed record SnapshotSourcePublicationEntry(string SourceType, Uuid RecordId, Uuid VersionId,
    long Version, string LogicalSha256, SnapshotSourceBlob Content);

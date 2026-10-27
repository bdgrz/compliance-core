namespace Bdgrz.Compliance.Features.Snapshots.Models;

/// <summary>Exact physical bytes of a logical frozen source; a Merkle identity is not the physical file hash.</summary>
public sealed record SnapshotSourceBlob(string Sha256, long Bytes, string MediaType, string FileExtension,
    string Description);

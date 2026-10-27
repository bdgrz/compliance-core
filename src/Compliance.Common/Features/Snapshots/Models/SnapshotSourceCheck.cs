namespace Bdgrz.Compliance.Features.Snapshots.Models;

public sealed record SnapshotSourceCheck(string Status, string ExpectedContentSha256,
    string? ObservedContentSha256);

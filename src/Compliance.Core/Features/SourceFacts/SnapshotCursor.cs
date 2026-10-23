namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>
/// Per-selector continuation state, safe to persist in a technical process record. The fingerprint and watermark
/// belong to this one selector's broker snapshot and are never valid for another selector. After a complete scan
/// <see cref="NextGlobalOffset"/> equals the cut watermark, so an empty scan checkpoints zero.
/// </summary>
public sealed record SnapshotCursor(string Route, ulong NextGlobalOffset, ulong? Fingerprint,
    ulong? CapturedWatermark);

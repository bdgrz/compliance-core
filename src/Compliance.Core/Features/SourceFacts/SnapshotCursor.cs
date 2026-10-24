namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>
/// Per-selector continuation state, safe to persist in a technical process record. The fingerprint and watermark
/// belong to this selector, tenant scope and committed cut and are never valid for another. After a complete scan
/// <see cref="NextGlobalOffset"/> equals the cut watermark, so an empty scan checkpoints zero.
/// </summary>
public sealed record SnapshotCursor(string Route, ulong NextGlobalOffset, ulong? Fingerprint,
    ulong? CapturedWatermark, string TenantScope, ulong CutWatermarkExclusive);

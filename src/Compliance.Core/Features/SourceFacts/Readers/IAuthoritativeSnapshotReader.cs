using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.SourceFacts.Readers;

/// <summary>
/// Backend-internal reader that captures one committed global cut and then reads required native facts at or below
/// it. It never appends and grants no authority; it is not a new broker snapshot guarantee.
/// </summary>
public interface IAuthoritativeSnapshotReader
{
    /// <summary>Captures the exclusive global frontier from an unfiltered global read. Zero is a valid frontier.</summary>
    /// <exception cref="SourceFactVerificationException">The broker did not report a watermark.</exception>
    Task<SnapshotCapture> CaptureAsync(CancellationToken ct = default);

    /// <summary>
    /// Scans one global selector from <paramref name="cursor"/> (or the beginning) until coverage reaches the cut,
    /// keeping only records of <paramref name="tenantScope"/> with a global position below the cut.
    /// </summary>
    /// <exception cref="SourceFactVerificationException">
    /// Coverage is incomplete: a filtered or missing relevant record, a snapshot older than the cut, a cursor from
    /// another selector, or a record without offsets.
    /// </exception>
    Task<SnapshotReadResult> ReadAtAsync(SnapshotSelector selector, string tenantScope, AuthoritativeReadCut cut,
        SnapshotCursor? cursor = null, CancellationToken ct = default);
}

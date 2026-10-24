using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>
/// A captured global cut and the anchor read's cursor fingerprint. The fingerprint is retained for diagnosis only;
/// it is never passed to a derived selector.
/// </summary>
public sealed record SnapshotCapture(AuthoritativeReadCut Cut, ulong? AnchorFingerprint);

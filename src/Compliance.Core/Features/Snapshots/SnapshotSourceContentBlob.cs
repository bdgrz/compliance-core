using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Snapshots;

/// <summary>Verified immutable business bytes included in a physical snapshot source envelope.</summary>
sealed record SnapshotSourceContentBlob(ImmutableContentRef Reference, byte[] Bytes);

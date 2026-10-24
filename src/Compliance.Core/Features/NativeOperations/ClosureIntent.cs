using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>
///     The frozen terms of a parent closure: result admission is sealed, every previously admitted child is drained,
///     and then the complete exact child uses are held. An abort atomically fences the parent finalization before any
///     source settlement.
/// </summary>
public sealed record ClosureIntent(DurableIntentHeader Header, ClosureParentKind ParentKind, Uuid ParentId,
    ulong ParentGeneration, ImmutableSelectionManifest AdmittedChildren, ImmutableContentRef AuthoredDecision,
    ImmutableSelectionManifest SourceUses, ImmutableContentRef? WaiverProof)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "closure-intent.v1";
}

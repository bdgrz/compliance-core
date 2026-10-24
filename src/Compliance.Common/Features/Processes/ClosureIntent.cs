using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Processes;

/// <summary>
///     The frozen terms of one closure: the sealed parent generation, the complete admitted child manifest, the
///     authored decision, the complete exact source uses and the separation-of-duties waiver proof when one was
///     required. Every closure receiver copies it; none re-authors it from current state.
/// </summary>
public sealed record ClosureIntent(DurableIntentHeader Header, ClosureParentKind ParentKind, Uuid ParentId,
    ulong ParentGeneration, ImmutableSelectionManifest AdmittedChildren, ImmutableContentRef AuthoredDecision,
    ImmutableSelectionManifest SourceUses, ImmutableContentRef? WaiverProof)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "closure-intent.v1";
}

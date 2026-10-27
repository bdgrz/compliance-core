using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>
///     The durable intent frozen when a parent admits its closure: the authored decision, the complete drained set of
///     children admitted before the seal, and the exact source uses the closure must hold. Every receiver of the
///     closure process reads it; none trusts what a command states about it.
/// </summary>
/// <param name="Header">The values frozen at first native admission.</param>
/// <param name="ParentKind">Which parent admission this closes.</param>
/// <param name="ParentId">The parent resource.</param>
/// <param name="ParentGeneration">The frozen admission generation of the parent.</param>
/// <param name="AdmittedChildren">Child admissions accepted before the seal; they drain before closure.</param>
/// <param name="AuthoredDecision">The authored closing decision as verified content.</param>
/// <param name="SourceUses">The exact source requirements to hold, in canonical key order.</param>
/// <param name="WaiverProof">The separation-of-duties waiver proof, when one applies.</param>
public sealed record ClosureIntent(DurableIntentHeader Header, ClosureParentKind ParentKind, Uuid ParentId,
    ulong ParentGeneration, ImmutableSelectionManifest AdmittedChildren, ImmutableContentRef AuthoredDecision,
    ImmutableSelectionManifest SourceUses, ImmutableContentRef? WaiverProof)
{
    /// <summary>Schema of the frozen intent content.</summary>
    public const string Schema = "closure-intent.v1";
}

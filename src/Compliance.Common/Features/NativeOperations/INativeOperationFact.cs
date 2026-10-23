using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>
/// A native business fact that accepts one logical operation on its owner stream. The owner's reducers hand every such
/// fact to its acceptance component so the selected logical id is bound to exactly one accepted fact.
/// </summary>
public interface INativeOperationFact
{
    /// <summary>Provenance binding the fact to its logical operation, actor, episode and authored intent.</summary>
    OperationProvenance Operation { get; }

    /// <summary>Frozen first response of a root admission; null on facts that are not root acceptances.</summary>
    ImmutableContentRef? FirstResponse { get; }
}

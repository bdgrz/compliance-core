using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     The complete typed inputs of one readiness evaluation, frozen once when the run is admitted. Each large part is
///     verified immutable content, so the computation, a replay and a rerun read exactly these inputs and no live
///     source. <paramref name="SourceCuts" /> lists the authoritative read cuts the source scan captured.
/// </summary>
public sealed record ReadinessInputSnapshot(Uuid ProgramId, DateTimeOffset AsOf, Uuid? EditionId,
    ImmutableContentRef? CatalogEdition, ImmutableContentRef Criteria, ImmutableContentRef Mappings,
    ImmutableContentRef EffectiveControls, ImmutableContentRef Sources, ImmutableSelectionManifest SourceCuts)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "readiness-input-snapshot.v1";
}

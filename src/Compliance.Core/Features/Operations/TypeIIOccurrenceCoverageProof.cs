using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The coverage of one period's recurring occurrences: the canonical ids that were selected, those whose
///     materialization is proven, those selected but not materialized, and the period's expected recurring occurrences
///     that no generation selected. Close requires both missing lists to be empty.
/// </summary>
public sealed record TypeIIOccurrenceCoverageProof(IReadOnlyList<Uuid> Selected, IReadOnlyList<Uuid> Materialized,
    IReadOnlyList<Uuid> NotMaterialized, IReadOnlyList<Uuid> NotSelected)
{
    /// <summary>True only when every expected recurring occurrence is selected and every selected one is materialized.</summary>
    public bool IsComplete => NotMaterialized.Count == 0 && NotSelected.Count == 0;
}

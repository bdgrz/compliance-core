namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>A bounded page of published editions as of the projection checkpoint.</summary>
/// <param name="Coverage">Whether initial seed setup is complete at the checkpoint; pending does not invalidate other returned editions.</param>
public sealed record RuntimeCriteriaEditionsPage(string Generation,
    IReadOnlyList<RuntimeCriteriaEdition> Items, string? NextCursor, string Coverage);

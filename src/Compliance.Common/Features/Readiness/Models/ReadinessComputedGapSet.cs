namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>One owning context's exact inputs, computed gaps and source versions for a program.</summary>
public sealed record ReadinessComputedGapSet(string Family, IReadOnlyList<ReadinessInputView> Inputs,
    IReadOnlyList<ReadinessGapView> Gaps, IReadOnlyList<ReadinessSourceReference> SourceRecordVersions);

namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>Canonical inputs binding contributed source versions and explanations to the original rule result.</summary>
public sealed record ReadinessComputedGapFingerprint(string OriginalInputFingerprint,
    IReadOnlyList<ReadinessComputedGapSet> ComputedInputs);

namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>The reproducible completeness and verdict of an assessment's recorded inputs.</summary>
public sealed record ReadinessCompleteness(bool IsComplete, string Verdict,
    IReadOnlyList<ReadinessIncompleteInput> IncompleteInputs);

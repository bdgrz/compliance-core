using Bdgrz.Compliance.Features.Readiness.Models;

namespace Bdgrz.Compliance.Features.Readiness.Rules;

/// <summary>Unknown and draft inputs take precedence over a known unmet rule.</summary>
public static class ReadinessCompletenessRules
{
    public static ReadinessCompleteness Evaluate(IReadOnlyList<ReadinessInputView> inputs,
        IReadOnlyList<ReadinessGapView> gaps)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(gaps);
        var incomplete = inputs.Where(static input => input.Status != "assessed")
            .Select(static input => new ReadinessIncompleteInput(input.Family, input.Status, input.Explanation))
            .ToArray();
        return new ReadinessCompleteness(incomplete.Length == 0,
            incomplete.Length > 0 ? "incomplete" : gaps.Count > 0 ? "not_ready" : "ready", incomplete);
    }
}

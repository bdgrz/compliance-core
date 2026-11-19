using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Readiness.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Rules;

/// <summary>Only a named source can replace its unknown-family placeholder.</summary>
public static class ReadinessComputedGapComposition
{
    public static ReadinessEvaluation Compose(Uuid programId, ReadinessEvaluation original,
        IReadOnlyList<ReadinessComputedGapSet> contributions)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(contributions);
        var ordered = contributions.OrderBy(static source => source.Family, StringComparer.Ordinal).ToArray();
        if (ordered.Select(static source => source.Family).Distinct(StringComparer.Ordinal).Count() != ordered.Length ||
            ordered.Any(static source => string.IsNullOrWhiteSpace(source.Family) || source.Inputs.Count == 0 ||
                source.Inputs.Any(input => input.Family != source.Family ||
                    input.Status is not ("assessed" or "unknown" or "draft" or "not_assessed" or "truncated")) ||
                source.Gaps.Any(static gap => gap.GapId == Uuid.Empty || string.IsNullOrWhiteSpace(gap.RuleId) ||
                    string.IsNullOrWhiteSpace(gap.Subject) || string.IsNullOrWhiteSpace(gap.Explanation))))
            throw new ArgumentException("A computed-gap source must provide its own explicit, bounded family answer.", nameof(contributions));
        var families = ordered.Select(static source => source.Family).ToHashSet(StringComparer.Ordinal);
        var inputs = original.Inputs.Where(input => !families.Contains(input.Family))
            .Concat(ordered.SelectMany(static source => source.Inputs)).ToArray();
        var replaced = ordered.Select(source => ReadinessRules.GapIdFor(programId, ReadinessRules.SourceFamilyAssessed, source.Family)).ToHashSet();
        var gaps = original.Gaps.Where(gap => !replaced.Contains(gap.GapId)).ToDictionary(static gap => gap.GapId);
        foreach (var gap in ordered.SelectMany(static source => source.Gaps))
            gaps[gap.GapId] = gap;
        return original with
        {
            Inputs = inputs,
            Gaps = gaps.Values.OrderBy(static gap => gap.GapId.ToString(), StringComparer.Ordinal).ToArray(),
            InputFingerprint = ordered.Length == 0 ? original.InputFingerprint :
                AuthoredIntentDigest.Compute(new ReadinessComputedGapFingerprint(original.InputFingerprint, ordered)),
        };
    }
}

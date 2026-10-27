using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     The business rules of a management readiness decision and of a Type I entry decision over the frozen gap set with
///     the plan each gap had at admission. Admission evaluates them on current state; the completing receiver evaluates
///     the same rules again at the decision instant over the held gap plans, so an admitted decision can never be
///     recorded against facts that no longer satisfy them.
/// </summary>
static class ReadinessDecisionRules
{
    public const int MaximumTextLength = 4000;

    /// <summary>The first rule the decision violates, or null when it satisfies every rule.</summary>
    /// <param name="kind">The decision kind.</param>
    /// <param name="outcome">The authored outcome.</param>
    /// <param name="rationale">The authored rationale.</param>
    /// <param name="gaps">The complete gap set of the assessment with each gap's plan.</param>
    /// <param name="acknowledged">The acknowledged gap identities (Type I entry only).</param>
    /// <param name="ruleVersion">The rule version the assessment used.</param>
    /// <param name="decidedOutcome">The outcome of the readiness decision already recorded on the assessment, if any.</param>
    /// <param name="enteredType1">True when an approved Type I entry decision already exists on the program.</param>
    /// <param name="independent">True when the decider did not run the assessment or an exact waiver proof covers it.</param>
    public static CommandFailure? Validate(ReadinessDecisionKind kind, string outcome, string rationale,
        IReadOnlyList<ReadinessGapView> gaps, IReadOnlyCollection<Uuid> acknowledged, string ruleVersion,
        string? decidedOutcome, bool enteredType1, bool independent)
    {
        ArgumentNullException.ThrowIfNull(gaps);
        ArgumentNullException.ThrowIfNull(acknowledged);
        return kind == ReadinessDecisionKind.Readiness
            ? ValidateReadiness(outcome, rationale, gaps, independent)
            : ValidateTypeIEntry(outcome, rationale, gaps, acknowledged, ruleVersion, decidedOutcome, enteredType1,
                independent);
    }

    /// <summary>True when a text is present and within the maximum length once trimmed.</summary>
    public static bool IsBoundedText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= MaximumTextLength;

    static CommandFailure? ValidateReadiness(string outcome, string rationale, IReadOnlyList<ReadinessGapView> gaps,
        bool independent)
    {
        if (!independent)
            return CommandFailure.ActorProhibited("The member who ran an assessment cannot decide it.");
        if (outcome is not (ProgramReadiness.Proceed or ProgramReadiness.DoNotProceed) || !IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "A readiness decision requires an outcome of proceed or do_not_proceed and a rationale of at most 4000 characters.");
        return outcome == ProgramReadiness.Proceed && gaps.Any(static gap => gap.Plan is null)
            ? CommandFailure.StateConflict("Every gap needs an owner and target date before management can proceed.")
            : null;
    }

    static CommandFailure? ValidateTypeIEntry(string outcome, string rationale, IReadOnlyList<ReadinessGapView> gaps,
        IReadOnlyCollection<Uuid> acknowledged, string ruleVersion, string? decidedOutcome, bool enteredType1,
        bool independent)
    {
        if (enteredType1)
            return CommandFailure.StateConflict("The program already entered Type I.");
        if (!independent)
            return CommandFailure.ActorProhibited("The member who ran an assessment cannot sign its Type I entry decision.");
        if (outcome is not (ProgramReadiness.Approve or ProgramReadiness.ApproveWithExceptions or ProgramReadiness.Defer) ||
            !IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "A Type I entry decision requires an outcome of approve, approve_with_exceptions, or defer and a rationale of at most 4000 characters.");
        var gapIds = gaps.Select(static gap => gap.GapId).ToHashSet();
        if (acknowledged.Any(id => !gapIds.Contains(id)))
            return CommandFailure.InvalidContent("Acknowledgements may name only gaps of this assessment.");
        if (outcome == ProgramReadiness.Defer)
            return null;
        if (ruleVersion != ReadinessRules.Version)
            return CommandFailure.StateConflict("The assessment used superseded readiness rules; run a new assessment.");
        if (decidedOutcome != ProgramReadiness.Proceed)
            return CommandFailure.StateConflict(
                "Management must decide to proceed on the assessment before Type I entry is approved.");
        if (outcome == ProgramReadiness.Approve && gaps.Count > 0)
            return CommandFailure.StateConflict(
                "Unresolved gaps remain; approve with exceptions and acknowledge each one, or defer.");
        if (outcome != ProgramReadiness.ApproveWithExceptions)
            return null;
        if (gaps.Count == 0)
            return CommandFailure.InvalidContent("No gaps remain; approve without exceptions.");
        if (!gapIds.SetEquals(acknowledged))
            return CommandFailure.StateConflict("Every unresolved gap must be explicitly acknowledged.");
        return gaps.Any(static gap => gap.Plan is null)
            ? CommandFailure.StateConflict("Every acknowledged gap needs an owner and target date.")
            : null;
    }
}

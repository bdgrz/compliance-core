using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     The product rules of an evaluation: a complete procedure, coherent step results, an explicit disposition of
///     every minor deviation, assertion conclusions the step results support, and independent review. They decide over
///     the folded <see cref="ControlEvaluationState" /> and build the content a fact references; the evaluation stream
///     re-checks its logical revision when the fact is appended.
/// </summary>
public static class ControlEvaluationRules
{
    const int MaximumTextLength = 4000;
    const int MaximumSteps = 100;
    const int MaximumItems = 50;

    static readonly string[] Methods = ["inquiry", "inspection", "observation", "reperformance"];

    /// <summary>The deterministic id of a procedure step.</summary>
    public static Uuid StepId(Uuid evaluationId, int index) => Uuid.CreateVersion5(evaluationId,
        "step-" + index.ToString(CultureInfo.InvariantCulture));

    /// <summary>The deterministic id of the deviation a not_met step raises.</summary>
    public static Uuid DeviationId(Uuid evaluationId, Uuid stepId) =>
        Uuid.CreateVersion5(evaluationId, "deviation-" + stepId);

    /// <summary>Validates a procedure: 1 to 100 steps, each with a known assertion and method and 1 to 50 inspected items.</summary>
    public static CommandFailure? ValidateProcedure(IReadOnlyList<EvaluationProcedureStep>? steps)
    {
        if (steps is null || steps.Count is < 1 or > MaximumSteps || steps.Any(static step =>
                step is null || !ControlEvaluationStates.Assertions.Contains(step.Assertion) ||
                !Methods.Contains(step.Method) || !IsBoundedText(step.ExpectedCondition) ||
                step.InspectedItems is null || step.InspectedItems.Count is < 1 or > MaximumItems))
            return CommandFailure.InvalidContent(
                "A procedure needs 1 to 100 steps, each with an assertion of design, implementation, or evidence_sufficiency, a method of inquiry, inspection, observation, or reperformance, an expected condition, and 1 to 50 inspected items.");
        if (steps.Any(static step => !AreItems(step.InspectedItems, required: true)))
            return CommandFailure.InvalidContent(
                "Each inspected item needs a kind of record, artifact, boundary, commitment, risk, criterion, control, policy, provider, or evidence, plus an exact reference and version.");
        return null;
    }

    /// <summary>
    ///     The conclusions the step results support. No steps or an untested step cannot be concluded effective; a
    ///     material deviation is ineffective; a minor one is at best effective_with_exceptions. An evaluator may always
    ///     conclude more severely.
    /// </summary>
    internal static string[] AllowedConclusions(ControlEvaluationStepView[] steps,
        IReadOnlyList<EvaluationDeviationView> deviations)
    {
        var stepIds = steps.Select(static step => step.StepId).ToHashSet();
        var related = deviations.Where(deviation => stepIds.Contains(deviation.StepId)).ToArray();
        if (steps.Length == 0)
            return [ControlEvaluationStates.NotTested];
        if (related.Any(static deviation => deviation.Classification == ControlEvaluationStates.Material))
            return [ControlEvaluationStates.Ineffective];
        if (steps.Any(static step => step.Result!.Result == ControlEvaluationStates.NotTested))
            return [ControlEvaluationStates.NotTested, ControlEvaluationStates.Ineffective];
        return related.Length > 0
            ? [ControlEvaluationStates.EffectiveWithExceptions, ControlEvaluationStates.Ineffective]
            : [ControlEvaluationStates.Effective, ControlEvaluationStates.EffectiveWithExceptions,
                ControlEvaluationStates.Ineffective];
    }

    /// <summary>The overall result: ineffective dominates, then not_tested, then exceptions.</summary>
    public static string Overall(IEnumerable<string> conclusions)
    {
        var all = conclusions.ToArray();
        return all.Contains(ControlEvaluationStates.Ineffective) ? ControlEvaluationStates.Ineffective
            : all.Contains(ControlEvaluationStates.NotTested) ? ControlEvaluationStates.NotTested
            : all.Contains(ControlEvaluationStates.EffectiveWithExceptions)
                ? ControlEvaluationStates.EffectiveWithExceptions
                : ControlEvaluationStates.Effective;
    }

    /// <summary>The trimmed procedure steps frozen for an evaluation.</summary>
    public static IReadOnlyList<EvaluationProcedureStep> Clean(IReadOnlyList<EvaluationProcedureStep> steps) =>
        steps.Select(static step => new EvaluationProcedureStep(step.Assertion, step.Method,
            step.InspectedItems.Select(Clean).ToArray(), step.ExpectedCondition.Trim())).ToArray();

    /// <summary>
    ///     Checks that a retest may start from the folded predecessor: an accepted evaluation of the same control with
    ///     a material deviation, tested with its plan version or a successor.
    /// </summary>
    internal static CommandFailure? CheckRetest(ControlEvaluationState? predecessor, Uuid controlId, long planVersion)
    {
        if (predecessor is null || predecessor.ControlId != controlId)
            return CommandFailure.MissingRecord("The evaluation to retest was not found.");
        var accepted = predecessor.AcceptedSubmission;
        if (accepted is null || !accepted.Deviations.Any(static deviation =>
                deviation.Classification == ControlEvaluationStates.Material))
            return CommandFailure.StateConflict(
                "Only an accepted evaluation with a material deviation can be retested.");
        return planVersion < predecessor.PlanVersion
            ? CommandFailure.StateConflict("A retest must use its original plan version or a successor version.")
            : null;
    }

    /// <summary>Checks a step result and builds the result and the deviation it raises, if any.</summary>
    internal static CommandFailure? CheckStep(ControlEvaluationState state, RecordControlEvaluationStep request,
        Uuid actorMemberId, ActorReference actor, DateTimeOffset recordedAt, out EvaluationStepResultView? recorded,
        out EvaluationDeviationView? deviation)
    {
        (recorded, deviation) = (null, null);
        if (Writable(state, request.ExpectedRevision, actorMemberId) is { } blocked)
            return blocked;
        if (!state.HasStep(request.StepId))
            return CommandFailure.MissingRecord("The procedure step was not found.");
        if (request.Result is not (ControlEvaluationStates.Met or ControlEvaluationStates.NotMet or
                ControlEvaluationStates.NotTested) || !IsBoundedText(request.Rationale) ||
            !AreItems(request.InspectedItems, required: request.Result != ControlEvaluationStates.NotTested))
            return CommandFailure.InvalidContent(
                "A step result must be met, not_met, or not_tested, with a rationale and, unless not tested, the exact items inspected.");
        if (request.Result == ControlEvaluationStates.NotMet
                ? request.DeviationClassification is not (ControlEvaluationStates.Minor or
                    ControlEvaluationStates.Material) || !IsBoundedText(request.DeviationDescription)
                : request.DeviationClassification is not null || request.DeviationDescription is not null)
            return CommandFailure.InvalidContent(
                "A not_met step requires a deviation classified minor or material with a description; other results cannot carry one.");
        if (request.Result == ControlEvaluationStates.NotMet)
        {
            // Stable per evaluation step, so a resubmitted deviation reuses its finding.
            var deviationId = DeviationId(state.EvaluationId, request.StepId);
            var material = request.DeviationClassification == ControlEvaluationStates.Material;
            deviation = new EvaluationDeviationView(deviationId, request.StepId, state.Round,
                request.DeviationClassification!, request.DeviationDescription!.Trim(),
                material ? ControlEvaluationStates.PendingRouting : ControlEvaluationStates.PendingDisposition,
                material ? Uuid.CreateVersion5(deviationId, "finding") : null, null, null, null, actor, recordedAt);
        }

        recorded = new EvaluationStepResultView(state.Round, request.Result, request.Rationale.Trim(),
            request.InspectedItems.Select(Clean).ToArray(), actor, recordedAt);
        return null;
    }

    /// <summary>Checks the disposition of a current minor deviation.</summary>
    internal static CommandFailure? CheckDisposition(ControlEvaluationState state,
        DisposeControlEvaluationDeviation request, Uuid actorMemberId, DateTimeOffset disposedAt,
        SeparationOfDutiesWaiver? waiver)
    {
        if (Writable(state, request.ExpectedRevision, actorMemberId) is { } blocked)
            return blocked;
        if (state.CurrentDeviations().FirstOrDefault(deviation => deviation.DeviationId == request.DeviationId) is not
            { } found)
            return CommandFailure.MissingRecord("The deviation is not a current deviation of this evaluation.");
        if (found.Status == ControlEvaluationStates.Dispositioned)
            return CommandFailure.StateConflict("The deviation already has a disposition.");
        if (found.Classification != ControlEvaluationStates.Minor)
            return CommandFailure.StateConflict(
                "A material deviation routes to a finding and a retest; it cannot be dispositioned.");
        if (request.Disposition is not (ControlEvaluationStates.Corrected or ControlEvaluationStates.AcceptedWithWaiver) ||
            !IsBoundedText(request.Rationale) ||
            request.Disposition == ControlEvaluationStates.Corrected && waiver is not null ||
            request.Disposition == ControlEvaluationStates.AcceptedWithWaiver && waiver is null)
            return CommandFailure.InvalidContent(
                "A disposition is corrected, or accepted_with_waiver naming an approved waiver, with a rationale.");
        if (waiver is not null && (waiver.TenantId != state.TenantId || !waiver.Allows(
                new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.ControlEvaluation,
                    state.EvaluationId, request.DeviationId, state.Round, SeparationOfDutiesActions.Approve),
                actorMemberId, disposedAt)))
            return CommandFailure.StateConflict(
                "The waiver is not approved and active for this evaluator, deviation, and round.");
        return null;
    }

    /// <summary>Checks a submission and builds the submitted round.</summary>
    internal static CommandFailure? CheckSubmit(ControlEvaluationState state, SubmitControlEvaluation request,
        Uuid actorMemberId, ActorReference actor, DateTimeOffset submittedAt, out EvaluationSubmissionView? submission)
    {
        submission = null;
        if (Writable(state, request.ExpectedRevision, actorMemberId) is { } blocked)
            return blocked;
        var steps = state.Steps();
        if (steps.Any(static step => step.Result is null))
            return CommandFailure.StateConflict("Record a result for every procedure step first.");
        var round = state.CurrentDeviations();
        if (round.Any(static deviation => deviation.Status == ControlEvaluationStates.PendingDisposition))
            return CommandFailure.StateConflict("Every minor deviation needs a disposition first.");
        var conclusions = request.Conclusions;
        if (conclusions is null || conclusions.Count != ControlEvaluationStates.Assertions.Count ||
            conclusions.Any(static conclusion => conclusion is null || !IsBoundedText(conclusion.Rationale)) ||
            ControlEvaluationStates.Assertions.Any(assertion =>
                conclusions.Count(conclusion => conclusion.Assertion == assertion) != 1))
            return CommandFailure.InvalidContent(
                "Conclude each of design, implementation, and evidence_sufficiency exactly once with a rationale.");
        foreach (var conclusion in conclusions)
        {
            var allowed = AllowedConclusions(steps.Where(step => step.Assertion == conclusion.Assertion).ToArray(),
                round);
            if (!allowed.Contains(conclusion.Conclusion))
                return CommandFailure.InvalidContent(
                    $"The {conclusion.Assertion} conclusion must be one of {string.Join(", ", allowed)} given its step results and deviations.");
        }

        var clean = ControlEvaluationStates.Assertions
            .Select(assertion => conclusions.Single(conclusion => conclusion.Assertion == assertion))
            .Select(static conclusion => conclusion with { Rationale = conclusion.Rationale.Trim() }).ToArray();
        submission = new EvaluationSubmissionView(state.Round, clean, Overall(clean.Select(static c => c.Conclusion)),
            steps, round, actor, submittedAt);
        return null;
    }

    /// <summary>Checks the independent review of the submitted round and builds the decision.</summary>
    internal static CommandFailure? CheckReview(ControlEvaluationState state, ReviewControlEvaluation request,
        Uuid reviewerMemberId, ActorReference reviewer, DateTimeOffset reviewedAt, SeparationOfDutiesWaiver? waiver,
        out ControlEvaluationReviewView? review)
    {
        review = null;
        if (request.ExpectedRevision != state.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("control evaluation", state.Revision));
        if (state.State != ControlEvaluationStates.Submitted)
            return CommandFailure.StateConflict("The evaluation has no submission awaiting review.");
        var scope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.ControlEvaluation,
            state.EvaluationId, state.EvaluationId, state.Round, SeparationOfDutiesActions.Review);
        if (waiver is not null && (waiver.TenantId != state.TenantId || !waiver.Allows(scope, reviewerMemberId, reviewedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and evaluation round.");
        if (state.EvaluatorMemberId == reviewerMemberId && waiver is null)
            return CommandFailure.ActorProhibited(
                "The evaluator cannot review their own evaluation without an approved, active separation-of-duties waiver.");
        if (request.Decision is not (ControlEvaluationStates.Accepted or ControlEvaluationStates.Rejected or
                ControlEvaluationStates.ChangesRequested) || !IsBoundedText(request.Rationale))
            return CommandFailure.InvalidContent(
                "A review decision is accepted, rejected, or changes_requested with a rationale.");
        review = new ControlEvaluationReviewView(request.CommandId, state.Round, request.Decision,
            request.Rationale.Trim(), reviewerMemberId, reviewer, reviewedAt, waiver?.Id);
        return null;
    }

    static CommandFailure? Writable(ControlEvaluationState state, long expectedRevision, Uuid actorMemberId)
    {
        if (state.EvaluatorMemberId != actorMemberId)
            return CommandFailure.ActorProhibited("Only the evaluator may record this evaluation.");
        if (expectedRevision != state.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("control evaluation", state.Revision));
        return state.State != ControlEvaluationStates.InProgress
            ? CommandFailure.StateConflict("The evaluation is not open for evaluator work.")
            : null;
    }

    static bool AreItems(IReadOnlyList<EvaluationInspectedItem>? items, bool required) =>
        items is not null && (!required || items.Count > 0) && items.Count <= MaximumItems &&
        items.All(static item => item is not null && (item.Kind is "record" or "artifact" or "boundary" or
            "commitment" or "risk" or "criterion" or "control" or "policy" or "provider" or "evidence") &&
            !string.IsNullOrWhiteSpace(item.Reference) && item.Reference.Trim().Length <= 2000 &&
            !string.IsNullOrWhiteSpace(item.Version) && item.Version.Trim().Length <= 200);

    static EvaluationInspectedItem Clean(EvaluationInspectedItem item) =>
        new(item.Kind, item.Reference.Trim(), item.Version.Trim());

    static bool IsBoundedText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= MaximumTextLength;
}

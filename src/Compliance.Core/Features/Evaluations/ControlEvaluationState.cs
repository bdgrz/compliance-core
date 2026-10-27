using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     The working state of one evaluation, folded from the facts of its own stream and the immutable content they
///     reference. The rules of every evaluation command decide over this state and are re-checked against the
///     stream's logical revision when a fact is appended.
/// </summary>
sealed class ControlEvaluationState(Uuid tenantId, Uuid programId, Uuid controlId, Uuid evaluationId,
    Uuid controlVersionId, Uuid planVersionId, long planVersion, IReadOnlyList<EvaluationProcedureStep> steps,
    Uuid evaluatorMemberId, ActorReference startedBy, DateTimeOffset startedAt, Uuid? retestOfEvaluationId,
    IReadOnlyList<Uuid> retestOfDeviationIds, NativeFactReference? retestPredecessor, long revision)
{
    public Uuid TenantId { get; } = tenantId;
    public Uuid ProgramId { get; } = programId;
    public Uuid ControlId { get; } = controlId;
    public Uuid EvaluationId { get; } = evaluationId;
    public Uuid ControlVersionId { get; } = controlVersionId;
    public Uuid PlanVersionId { get; } = planVersionId;
    public long PlanVersion { get; } = planVersion;
    public IReadOnlyList<EvaluationProcedureStep> Procedure { get; } = steps;
    public Uuid EvaluatorMemberId { get; } = evaluatorMemberId;
    public ActorReference StartedBy { get; } = startedBy;
    public DateTimeOffset StartedAt { get; } = startedAt;
    public Uuid? RetestOfEvaluationId { get; } = retestOfEvaluationId;
    public IReadOnlyList<Uuid> RetestOfDeviationIds { get; } = retestOfDeviationIds;
    public NativeFactReference? RetestPredecessor { get; } = retestPredecessor;
    public long Revision { get; set; } = revision;
    public string State { get; set; } = ControlEvaluationStates.InProgress;
    public int Round { get; set; } = 1;
    public Dictionary<Uuid, EvaluationStepResultView> Results { get; } = [];
    public List<EvaluationDeviationView> Deviations { get; } = [];
    public List<EvaluationSubmissionView> Submissions { get; } = [];
    public List<ControlEvaluationReviewView> Reviews { get; } = [];

    /// <summary>The event id and aggregate version of the fact that accepted the evaluation, once it is accepted.</summary>
    public (Uuid EventId, ulong AggregateVersion)? AcceptingFact { get; set; }

    /// <summary>The accepted round's submission, once the evaluation is accepted.</summary>
    public EvaluationSubmissionView? AcceptedSubmission =>
        State == ControlEvaluationStates.Accepted ? Submissions[^1] : null;

    /// <summary>A step result at the next revision, with the deviation it raised, if any.</summary>
    public void RecordStep(long revision, Uuid stepId, EvaluationStepResultView result, EvaluationDeviationView? deviation)
    {
        Revision = revision;
        Results[stepId] = result;
        Deviations.RemoveAll(existing => existing.StepId == stepId && existing.Round == Round);
        if (deviation is not null)
            Deviations.Add(deviation);
    }

    /// <summary>A deviation disposition at the next revision.</summary>
    public void DisposeDeviation(long revision, Uuid deviationId, string disposition, string rationale, Uuid? waiverId)
    {
        Revision = revision;
        var index = Deviations.FindIndex(deviation => deviation.DeviationId == deviationId);
        Deviations[index] = Deviations[index] with
        {
            Status = ControlEvaluationStates.Dispositioned,
            Disposition = disposition,
            DispositionRationale = rationale,
            WaiverId = waiverId,
        };
    }

    /// <summary>A submitted round at the next revision.</summary>
    public void Submit(long revision, EvaluationSubmissionView submission)
    {
        Revision = revision;
        State = ControlEvaluationStates.Submitted;
        Submissions.Add(submission);
    }

    /// <summary>A review decision at the next revision: accepting ends the evaluation, anything else opens a new round.</summary>
    public void Review(long revision, ControlEvaluationReviewView review)
    {
        Revision = revision;
        Reviews.Add(review);
        switch (review.Decision)
        {
            case ControlEvaluationStates.Accepted:
                State = ControlEvaluationStates.Accepted;
                break;
            case ControlEvaluationStates.Rejected:
                State = ControlEvaluationStates.InProgress;
                Round++;
                Results.Clear();
                break;
            default:
                State = ControlEvaluationStates.InProgress;
                Round++;
                break;
        }
    }

    /// <summary>An independent copy, used to shape the view an operation will return before it is appended.</summary>
    public ControlEvaluationState Clone()
    {
        var copy = new ControlEvaluationState(TenantId, ProgramId, ControlId, EvaluationId, ControlVersionId,
            PlanVersionId, PlanVersion, Procedure, EvaluatorMemberId, StartedBy, StartedAt, RetestOfEvaluationId,
            RetestOfDeviationIds, RetestPredecessor, Revision)
        {
            State = State,
            Round = Round,
            AcceptingFact = AcceptingFact,
        };
        foreach (var (stepId, result) in Results)
            copy.Results[stepId] = result;
        copy.Deviations.AddRange(Deviations);
        copy.Submissions.AddRange(Submissions);
        copy.Reviews.AddRange(Reviews);
        return copy;
    }

    /// <summary>The frozen procedure steps with their current-round results.</summary>
    public ControlEvaluationStepView[] Steps() => Procedure.Select((step, index) =>
    {
        var stepId = ControlEvaluationRules.StepId(EvaluationId, index);
        return new ControlEvaluationStepView(stepId, index, step.Assertion, step.Method, step.InspectedItems,
            step.ExpectedCondition, Results.GetValueOrDefault(stepId));
    }).ToArray();

    /// <summary>The latest deviation of each step whose current result is not_met.</summary>
    public EvaluationDeviationView[] CurrentDeviations() => Results
        .Where(static result => result.Value.Result == ControlEvaluationStates.NotMet)
        .Select(result => Deviations.Last(deviation => deviation.StepId == result.Key))
        .OrderBy(static deviation => deviation.DetectedAt).ToArray();

    /// <summary>Whether the procedure contains the step.</summary>
    public bool HasStep(Uuid stepId)
    {
        for (var index = 0; index < Procedure.Count; index++)
            if (ControlEvaluationRules.StepId(EvaluationId, index) == stepId)
                return true;
        return false;
    }

    /// <summary>The public view of this evaluation; <paramref name="retests"/> are the evaluations that retest it.</summary>
    public ControlEvaluationView ToView(IReadOnlyList<ControlEvaluationState> retests)
    {
        var ordered = retests.OrderBy(static other => other.StartedAt).ThenBy(static other => other.EvaluationId.ToString(),
            StringComparer.Ordinal).ToArray();
        var accepted = AcceptedSubmission;
        string retestStatus;
        if (accepted is null || !accepted.Deviations.Any(static deviation =>
                deviation.Classification == ControlEvaluationStates.Material))
            retestStatus = ControlEvaluationStates.NotRequired;
        else if (ordered.Length == 0)
            retestStatus = ControlEvaluationStates.Required;
        else
            retestStatus = ordered.Where(static retest => retest.State == ControlEvaluationStates.Accepted)
                .OrderBy(static retest => retest.Reviews[^1].ReviewedAt)
                .Select(static retest => retest.AcceptedSubmission!.Overall)
                .LastOrDefault() switch
            {
                null => ControlEvaluationStates.InProgress,
                ControlEvaluationStates.Effective or ControlEvaluationStates.EffectiveWithExceptions =>
                    ControlEvaluationStates.Passed,
                _ => ControlEvaluationStates.Failed,
            };
        return new ControlEvaluationView(TenantId, ProgramId, ControlId, EvaluationId, Revision, ControlVersionId,
            State, Round, EvaluatorMemberId, StartedBy, StartedAt, Steps(), Deviations.ToArray(),
            Submissions.ToArray(), Reviews.ToArray(), Reviews.LastOrDefault(), accepted?.Overall,
            RetestOfEvaluationId, RetestOfDeviationIds, retestStatus,
            ordered.Select(static other => other.EvaluationId).ToArray())
        {
            PlanVersionId = PlanVersionId,
            PlanVersion = PlanVersion,
        };
    }
}

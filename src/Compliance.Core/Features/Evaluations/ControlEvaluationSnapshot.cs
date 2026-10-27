using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     Every evaluation of one program composed from their own streams, with the fence they were read at. Retest
///     relationships are composed here from the retests' own facts; no evaluation stream records them.
/// </summary>
public sealed class ControlEvaluationSnapshot(IReadOnlyList<ControlEvaluationView> evaluations, SourceFence fence)
{
    /// <summary>Every evaluation in the order of the streams' first facts.</summary>
    public IReadOnlyList<ControlEvaluationView> Evaluations { get; } = evaluations;

    /// <summary>The fence the snapshot was read at.</summary>
    public SourceFence Fence { get; } = fence;

    /// <summary>One evaluation of the control, or null.</summary>
    public ControlEvaluationView? Read(Uuid controlId, Uuid evaluationId) => Evaluations.FirstOrDefault(
        evaluation => evaluation.ControlId == controlId && evaluation.EvaluationId == evaluationId);

    /// <summary>One evaluation by its exact identity, or null.</summary>
    public ControlEvaluationView? Read(Uuid evaluationId) =>
        Evaluations.FirstOrDefault(evaluation => evaluation.EvaluationId == evaluationId);

    /// <summary>A control's evaluations, newest first.</summary>
    public IReadOnlyList<ControlEvaluationView> ReadControl(Uuid controlId) => Evaluations
        .Where(evaluation => evaluation.ControlId == controlId)
        .OrderByDescending(static evaluation => evaluation.StartedAt)
        .ThenBy(static evaluation => evaluation.EvaluationId.ToString(), StringComparer.Ordinal).ToArray();

    /// <summary>Current evaluation rounds awaiting independent review, oldest first.</summary>
    public IReadOnlyList<ControlEvaluationView> AwaitingReview() => Evaluations
        .Where(static evaluation => evaluation.State == ControlEvaluationStates.Submitted)
        .OrderBy(static evaluation => evaluation.StartedAt)
        .ThenBy(static evaluation => evaluation.EvaluationId.ToString(), StringComparer.Ordinal).ToArray();

    /// <summary>The submitted deviation of an evaluation, or null when it was never submitted.</summary>
    public EvaluationDeviationView? SubmittedDeviation(Uuid controlId, Uuid evaluationId, Uuid deviationId) =>
        Read(controlId, evaluationId)?.Submissions.SelectMany(static submission => submission.Deviations)
            .LastOrDefault(deviation => deviation.DeviationId == deviationId);
}

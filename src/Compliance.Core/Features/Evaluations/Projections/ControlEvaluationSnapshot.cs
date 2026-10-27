using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Evaluations.Projections;

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

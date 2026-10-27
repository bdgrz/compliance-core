using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Resolves the deviation routing of an evaluation read.</summary>
static class ControlEvaluationSource
{
    public static RequestError EvaluationNotFound() =>
        new(RequestErrorKind.NotFound, "The control evaluation was not found.");

    /// <summary>Marks submitted material deviations whose finding now exists as routed.</summary>
    public static ControlEvaluationView WithRouting(ControlEvaluationView view,
        FindingReadSnapshot remediation)
    {
        static EvaluationDeviationView Route(EvaluationDeviationView deviation,
            FindingReadSnapshot remediation) =>
            deviation.FindingId is { } findingId && remediation.Contains(findingId)
                ? deviation with { Status = ControlEvaluationStates.RoutedToFinding }
                : deviation;

        return view with
        {
            Deviations = view.Deviations.Select(d => Route(d, remediation)).ToArray(),
            Submissions = view.Submissions.Select(submission => submission with
            {
                Deviations = submission.Deviations.Select(d => Route(d, remediation)).ToArray(),
            }).ToArray(),
        };
    }
}

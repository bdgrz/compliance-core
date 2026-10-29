using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.Evaluations.Services;

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

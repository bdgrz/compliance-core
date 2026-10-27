using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.Services;

/// <summary>
///     Folds the frozen rows of one target group, in original row order, against the group's single baseline.
///     Create establishes revision one, a link neither overwrites the application nor advances its revision, and a
///     retirement advances it once. The baseline is compared once; every later row is compared against the candidate
///     the earlier rows produce, never against a repeated baseline.
/// </summary>
static class ApplicationImportGroupFold
{
    public static Result<IReadOnlyList<long>> Fold(bool created, bool retired, long revision,
        IReadOnlyList<ApplicationImportPlannedRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var outcomes = new List<long>(rows.Count);
        foreach (var row in rows)
        {
            switch (row.Decision)
            {
                case "create_new" when !created && !retired && revision == 0 && row.ExpectedApplicationRevision is null:
                    (created, revision) = (true, 1);
                    break;
                case "link_existing" when created && !retired && row.ExpectedApplicationRevision == revision:
                    break;
                case "retire" when created && !retired && row.ExpectedApplicationRevision == revision:
                    (retired, revision) = (true, revision + 1);
                    break;
                default:
                    return Result<IReadOnlyList<long>>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The governed import target changed after its frozen plan."));
            }

            outcomes.Add(revision);
        }

        return Result<IReadOnlyList<long>>.Success(outcomes.AsReadOnly());
    }
}

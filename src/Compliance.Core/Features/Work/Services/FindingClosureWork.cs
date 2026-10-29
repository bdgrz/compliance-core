using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
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
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Services;

static class FindingClosureWork
{
    public const string Kind = "finding_closure_review";

    public static WorkCandidate? Candidate(FindingClosureWorkState finding)
    {
        if (finding.Closed || finding.Actions.Length == 0 ||
            finding.Actions.Any(static action => action.Status != FindingStates.Completed))
            return null;
        var excluded = finding.Actions.Select(static action => action.OwnerMemberId).ToHashSet();
        excluded.Add(finding.OwnerMemberId);
        foreach (var action in finding.Actions)
            if (action.CompletedByMemberId is { } completer)
                excluded.Add(completer);
        var identity = Uuid.CreateVersion5(finding.FindingId,
            $"finding-closure\n{finding.Revision.ToString(CultureInfo.InvariantCulture)}");
        return new WorkCandidate(WorkCandidate.IdFor(identity, Kind), Kind, finding.FindingId,
            null, finding.FindingId, $"Verify and close finding: {finding.Title}",
            $"Completed corrective work at finding revision {finding.Revision} awaits independent verification.",
            finding.DueOn, FindingStates.Materiality(finding.Severity), "close",
            $"/api/v1/tenants/{finding.TenantId}/programs/{finding.ProgramId}/findings/{finding.FindingId}/closures",
            new OperatingHolder(OperatingAuthority.ProgramManagerHolder, finding.ProgramId),
            null, excluded, finding.ChangedAt);
    }

    public static WorkCandidate? Candidate(FindingView finding) => Candidate(new FindingClosureWorkState(
        finding.TenantId, finding.ProgramId, finding.FindingId, finding.Revision, finding.Title,
        finding.Severity, finding.OwnerMemberId, finding.DueOn,
        finding.CorrectiveActions.Select(static action => new FindingClosureActionState(action.ActionId,
            action.OwnerMemberId, action.Status, action.CompletedByMemberId)).ToArray(),
        finding.Closure is not null, finding.History.Max(static entry => entry.At)));
}

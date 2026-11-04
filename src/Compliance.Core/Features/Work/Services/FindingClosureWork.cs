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
        if (finding.Closed || finding.Proposal is not { } proposal ||
            finding.Actions.Any(static action => action.Status is not (FindingStates.Completed or FindingStates.Cancelled)))
            return null;
        var identity = Uuid.CreateVersion5(finding.FindingId,
            $"finding-closure-review\n{proposal.ClosureId}\n{proposal.Revision.ToString(CultureInfo.InvariantCulture)}");
        return new WorkCandidate(WorkCandidate.IdFor(identity, Kind), Kind, finding.FindingId,
            null, finding.FindingId, $"Review finding closure: {finding.Title}",
            $"Closure proposal revision {proposal.Revision} awaits independent Compliance Lead review.",
            finding.DueOn, FindingStates.Materiality(finding.Severity), "review",
            $"/api/v1/tenants/{finding.TenantId}/programs/{finding.ProgramId}/findings/{finding.FindingId}/closure-proposals/{proposal.ClosureId}/revisions/{proposal.Revision}/reviews",
            new OperatingHolder(OperatingAuthority.ProgramManagerHolder, finding.ProgramId),
            null, proposal.Authors.ToHashSet(), proposal.ProposedAt);
    }

    public static WorkCandidate? Candidate(FindingView finding) => Candidate(new FindingClosureWorkState(
        finding.TenantId, finding.ProgramId, finding.FindingId, finding.Revision, finding.Title,
        finding.Severity, finding.OwnerMemberId, finding.DueOn,
        finding.CorrectiveActions.Select(static action => new FindingClosureActionState(action.ActionId,
            action.OwnerMemberId, action.Status, action.CompletedByMemberId, action.CancelledByMemberId,
            action.CancellationReason)).ToArray(),
        finding.Closure is not null, finding.History.Max(static entry => entry.At), Proposal(finding)));

    internal static FindingClosureProposalWorkState? Proposal(FindingView finding) =>
        finding.ClosureProposal is not { } proposal ? null :
            new FindingClosureProposalWorkState(proposal.ClosureId, proposal.ProposalRevision,
                proposal.Authors ?? [Uuid.Parse(proposal.ProposedBy.Id, CultureInfo.InvariantCulture)], proposal.ProposedAt);
}

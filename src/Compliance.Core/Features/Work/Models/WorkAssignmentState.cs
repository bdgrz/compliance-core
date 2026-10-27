using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>Recorded accountability for one work item: revision, assignee, escalation, and history.</summary>
public sealed record WorkAssignmentState(long Revision, Uuid? AssigneeMemberId, bool Escalated,
    IReadOnlyList<WorkAssignmentEntryView> History)
{
    public static WorkAssignmentState Empty { get; } = new(0, null, false, []);
}

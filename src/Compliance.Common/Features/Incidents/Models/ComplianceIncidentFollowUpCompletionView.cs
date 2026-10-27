using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Incidents.Models;

/// <summary>
///     The verified native completion of one follow-up work item: the work item and the proof of the committed
///     corrective-action completion that finished it.
/// </summary>
public sealed record ComplianceIncidentFollowUpCompletionView(
    Uuid WorkItemId,
    SourceFactProof<CorrectiveActionCompletedV2> Proof);

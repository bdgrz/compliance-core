using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>
///     The verified native completion of one follow-up work item: the work item and the proof of the committed
///     corrective-action completion that finished it.
/// </summary>
public sealed record ComplianceIncidentFollowUpCompletionView(
    Uuid WorkItemId,
    SourceFactProof<CorrectiveActionCompletedV2> Proof);

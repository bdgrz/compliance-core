using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Revises open accountable treatment work. <paramref name="ExpectedRevision" /> is the revision of the action
///     owner. HTTP-only.
/// </summary>
[Discriminator("bdgrz.risk.treatment_action.revise", 2)]
public sealed record ReviseRiskTreatmentAction(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    Uuid ActionId, long ExpectedRevision, string Title, string TargetState,
    string ExpectedEvidence, DateOnly DueOn, Uuid AccountableMemberId,
    IReadOnlyList<Uuid>? EvidenceRequestIds = null)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

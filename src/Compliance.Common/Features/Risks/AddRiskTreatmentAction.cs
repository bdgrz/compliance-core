using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Adds accountable work to a risk whose chosen treatment is not <c>accept</c>: a title, the state the work should
///     reach, the evidence that will show it, a due date, an active client member who is accountable, and optionally the
///     program evidence requests that will collect the evidence. <paramref name="ActionId" /> is the action identity and
///     the logical operation; creating an action requires <paramref name="ExpectedRevision" /> zero. HTTP-only.
/// </summary>
[Discriminator("bdgrz.risk.treatment_action.add", 2)]
public sealed record AddRiskTreatmentAction(Uuid ActionId, Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long ExpectedRevision, string Title, string TargetState, string ExpectedEvidence,
    DateOnly DueOn, Uuid AccountableMemberId, IReadOnlyList<Uuid>? EvidenceRequestIds = null)
    : IRequest<RiskTreatmentActionRegistration>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => ActionId;
}

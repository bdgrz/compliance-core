using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Cancels unfinished treatment work while retaining its history. <paramref name="ExpectedRevision" /> is the
///     revision of the action owner. HTTP-only.
/// </summary>
[Discriminator("bdgrz.risk.treatment_action.cancel", 2)]
public sealed record CancelRiskTreatmentAction(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    Uuid ActionId, long ExpectedRevision, string Rationale)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

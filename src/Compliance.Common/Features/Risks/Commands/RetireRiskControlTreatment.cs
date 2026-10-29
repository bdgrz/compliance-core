using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Risks.Commands;

/// <summary>
///     Withdraws an accepted control treatment assertion while keeping its history. A retirement never retires a newer
///     successor, because only the accepted treatment named by <paramref name="TreatmentId" /> can be retired.
/// </summary>
[Discriminator("bdgrz.risk.control_treatment.retire", 2)]
public sealed record RetireRiskControlTreatment(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    Uuid TreatmentId, long ExpectedRevision, string Rationale)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

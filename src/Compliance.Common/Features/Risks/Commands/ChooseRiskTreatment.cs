using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Risks.Commands;

[Discriminator("bdgrz.risk.treatment.choose", 1)]
public sealed record ChooseRiskTreatment(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long ExpectedRevision, string Kind, string Rationale)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

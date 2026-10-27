using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Commands;

[Discriminator("bdgrz.risk.assessment.record", 1)]
public sealed record RecordRiskAssessment(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long ExpectedRevision, long MethodVersion, string Phase, int Likelihood, int Impact,
    string Rationale) : IRequest<RiskAssessmentView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

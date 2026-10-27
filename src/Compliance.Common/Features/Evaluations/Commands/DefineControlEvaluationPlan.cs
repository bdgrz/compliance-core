using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Evaluations.Commands;

[Discriminator("bdgrz.control.evaluation.plan.define", 1)]
public sealed record DefineControlEvaluationPlan(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid ControlVersionId, long ExpectedVersion, string Objective,
    IReadOnlyList<EvaluationProcedureStep>? Steps,
    bool TesterIndependenceRequired)
    : IRequest<ControlEvaluationPlanVersionView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => ControlEvaluationPlanIdentity.For(TenantId, ProgramId, ControlId, ExpectedVersion);
}

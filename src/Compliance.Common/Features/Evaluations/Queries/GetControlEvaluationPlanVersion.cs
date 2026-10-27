using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations.Queries;

[Discriminator("bdgrz.control.evaluation.plan.version.get", 1)]
public sealed record GetControlEvaluationPlanVersion(Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, Uuid PlanVersionId)
    : IRequest<ControlEvaluationPlanVersionView>, IControlOperationRequest, ICallable;

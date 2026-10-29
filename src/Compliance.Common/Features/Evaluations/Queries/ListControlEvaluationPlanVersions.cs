using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
namespace Bdgrz.Compliance.Features.Evaluations.Queries;

[Discriminator("bdgrz.control.evaluation.plan.versions.list", 1)]
public sealed record ListControlEvaluationPlanVersions(Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, int Limit = 50, string? Cursor = null)
    : IRequest<Page<ControlEvaluationPlanVersionView>>, IControlOperationRequest, ICallable;

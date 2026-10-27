using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Evaluations.Queries;

[Discriminator("bdgrz.control.evaluation.get", 1)]
public sealed record GetControlEvaluation(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid EvaluationId)
    : IRequest<ControlEvaluationView>, IProgramReadRequest, ICallable;

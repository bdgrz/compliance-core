using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Queries;

[Discriminator("bdgrz.risk.evaluation.get", 1)]
public sealed record GetRiskEvaluation(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long? MinimumRevision = null)
    : IRequest<RiskEvaluationView>, IProgramScopedRequest, ICallable;

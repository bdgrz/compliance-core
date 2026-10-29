using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Queries;

[Discriminator("bdgrz.risk.governance.get", 1)]
public sealed record GetRiskGovernance(Uuid TenantId, Uuid ProgramId, Uuid RiskId)
    : IRequest<RiskGovernanceView>, IProgramScopedRequest, ICallable;

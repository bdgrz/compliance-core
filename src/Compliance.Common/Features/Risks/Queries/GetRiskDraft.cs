using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Queries;

[Discriminator("bdgrz.risk.draft.get", 1)]
public sealed record GetRiskDraft(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long? MinimumRevision = null) : IRequest<RiskDraftView>, IProgramScopedRequest, ICallable;

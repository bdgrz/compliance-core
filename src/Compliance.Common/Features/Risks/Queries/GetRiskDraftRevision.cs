using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Queries;

[Discriminator("bdgrz.risk.draft.revision.get", 1)]
public sealed record GetRiskDraftRevision(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision) : IRequest<RiskDraftRevisionView>, IProgramScopedRequest, ICallable;

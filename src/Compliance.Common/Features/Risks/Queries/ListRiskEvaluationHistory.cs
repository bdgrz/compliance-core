using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Queries;

[Discriminator("bdgrz.risk.evaluation.history.list", 1)]
public sealed record ListRiskEvaluationHistory(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    int? Limit = null, string? Cursor = null, long? MinimumRevision = null)
    : IRequest<Page<RiskEvaluationHistoryEntryView>>, IProgramScopedRequest, ICallable;

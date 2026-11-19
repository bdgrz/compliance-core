using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Submits one source-qualified criterion-summary correction.</summary>
public sealed class CorrectCriterionSummaryHandler(CriteriaEntryOperations operations)
    : IRequestHandler<CorrectCriterionSummary, CriteriaOperationReceipt>
{
    public ValueTask<Result<CriteriaOperationReceipt>> HandleAsync(
        IRequestContext<CorrectCriterionSummary> context, CancellationToken ct) => operations.CorrectSummaryAsync(context, ct);
}

using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Submits one source-qualified structural criterion proposal.</summary>
public sealed class ProposeCriterionChangeHandler(CriteriaEntryOperations operations)
    : IRequestHandler<ProposeCriterionChange, CriteriaOperationReceipt>
{
    public ValueTask<Result<CriteriaOperationReceipt>> HandleAsync(
        IRequestContext<ProposeCriterionChange> context, CancellationToken ct) => operations.ProposeAsync(context, ct);
}

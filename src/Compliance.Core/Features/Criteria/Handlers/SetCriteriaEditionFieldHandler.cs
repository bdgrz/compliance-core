using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Submits a source-checked edition metadata field proposal.</summary>
public sealed class SetCriteriaEditionFieldHandler(CriteriaEditionValueOperations operations)
    : IRequestHandler<SetCriteriaEditionField, CriteriaOperationReceipt>
{
    public ValueTask<Result<CriteriaOperationReceipt>> HandleAsync(IRequestContext<SetCriteriaEditionField> context,
        CancellationToken ct) => operations.SetFieldAsync(context, ct);
}

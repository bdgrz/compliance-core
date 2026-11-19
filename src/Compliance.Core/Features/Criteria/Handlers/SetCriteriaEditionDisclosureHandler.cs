using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Submits a source-checked edition support disclosure proposal.</summary>
public sealed class SetCriteriaEditionDisclosureHandler(CriteriaEditionValueOperations operations)
    : IRequestHandler<SetCriteriaEditionDisclosure, CriteriaOperationReceipt>
{
    public ValueTask<Result<CriteriaOperationReceipt>> HandleAsync(IRequestContext<SetCriteriaEditionDisclosure> context,
        CancellationToken ct) => operations.SetDisclosureAsync(context, ct);
}

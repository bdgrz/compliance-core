using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Admits and executes the exact criteria edition operation.</summary>
public sealed class BeginCriteriaPublicationHandler(CriteriaEditionOperations operations)
    : IRequestHandler<BeginCriteriaPublication, CriteriaOperationReceipt>
{
    public ValueTask<Result<CriteriaOperationReceipt>> HandleAsync(IRequestContext<BeginCriteriaPublication> context, CancellationToken ct) =>
        operations.PublishAsync(context, ct);
}

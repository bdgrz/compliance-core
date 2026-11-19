using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Queries;

/// <summary>Reads one exact field or disclosure operation outcome.</summary>
/// <remarks>Unknown source or unqualified outcome remains pending.</remarks>
[Discriminator("bdgrz.criteria.edition-value.operation.get", 1)]
public sealed record GetCriteriaEditionValueOperation(Uuid EditionId, string TargetKind, string? Field,
    string? Category, string? Code, Uuid OperationId)
    : IRequest<CriteriaOperationReceipt>, IPlatformOperatorRequest, ICallable;

using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Queries;

/// <summary>Reads the exact outcome of one edition- or entry-scoped operation.</summary>
/// <remarks>An unknown or unobserved outcome remains pending; absence is not a refusal.</remarks>
[Discriminator("bdgrz.criteria.operation.get", 1)]
public sealed record GetCriteriaOperation(Uuid EditionId, Uuid OperationId, string? Identifier = null)
    : IRequest<CriteriaOperationReceipt>, IPlatformOperatorRequest, ICallable;

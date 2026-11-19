using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Proposes one structural change to a single criterion identity.</summary>
/// <remarks>
/// The operation identity is scoped to the edition and identifier. For a point
/// of focus the parent identity and incarnation identify one exact criterion;
/// parent removal does not mutate this entry.
/// </remarks>
[Discriminator("bdgrz.criteria.entry.change.propose", 1)]
public sealed record ProposeCriterionChange(Uuid EditionId, Uuid DraftGeneration, string Identifier, Uuid OperationId,
    long ExpectedRevision, string Action, string? Kind, string? Category, string? ParentIdentifier,
    Uuid? ParentIncarnationId, string? Summary)
    : IRequest<CriteriaOperationReceipt>, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => OperationId;
}

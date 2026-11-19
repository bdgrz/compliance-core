using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Proposes one independently owned edition metadata field.</summary>
[Discriminator("bdgrz.criteria.edition-field.set", 1)]
public sealed record SetCriteriaEditionField(Uuid EditionId, Uuid DraftGeneration, string Field, Uuid OperationId,
    long ExpectedRevision, string? TextValue, bool? BooleanValue)
    : IRequest<CriteriaOperationReceipt>, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => OperationId;
}

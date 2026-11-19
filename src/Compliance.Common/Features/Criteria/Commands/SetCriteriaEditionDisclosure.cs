using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Proposes one independently owned support disclosure note.</summary>
[Discriminator("bdgrz.criteria.edition-disclosure.set", 1)]
public sealed record SetCriteriaEditionDisclosure(Uuid EditionId, Uuid DraftGeneration, string Category, string Code,
    Uuid OperationId, long ExpectedRevision, string Note)
    : IRequest<CriteriaOperationReceipt>, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => OperationId;
}

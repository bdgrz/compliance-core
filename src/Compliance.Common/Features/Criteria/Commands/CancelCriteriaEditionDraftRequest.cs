using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Cancels an exact initial-label attempt that has not completed.</summary>
[Discriminator("bdgrz.criteria.edition.draft.cancel", 1)]
public sealed record CancelCriteriaEditionDraftRequest(Uuid EditionId, Uuid OperationId, Uuid TargetOperationId)
    : IRequest<CriteriaOperationReceipt>, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => OperationId;
}

using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Cancels an exact uncommitted draft-label change.</summary>
[Discriminator("bdgrz.criteria.edition.rename.cancel", 1)]
public sealed record CancelCriteriaEditionRename(Uuid EditionId, Uuid OperationId, Uuid RenameOperationId)
    : IRequest<CriteriaOperationReceipt>, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => OperationId;
}

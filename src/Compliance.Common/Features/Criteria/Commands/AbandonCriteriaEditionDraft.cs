using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Permanently abandons a draft edition.</summary>
[Discriminator("bdgrz.criteria.edition.draft.abandon", 1)]
public sealed record AbandonCriteriaEditionDraft(Uuid EditionId, Uuid OperationId)
    : IRequest<CriteriaOperationReceipt>, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => OperationId;
}

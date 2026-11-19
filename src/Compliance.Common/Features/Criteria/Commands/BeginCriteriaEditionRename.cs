using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Starts a draft-label change under a client-supplied operation identity.</summary>
[Discriminator("bdgrz.criteria.edition.rename.begin", 1)]
public sealed record BeginCriteriaEditionRename(Uuid EditionId, Uuid OperationId, string Label)
    : IRequest<CriteriaOperationReceipt>, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => OperationId;
}

using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Starts publication using one exact criterion incarnation as its witness.</summary>
[Discriminator("bdgrz.criteria.edition.publication.begin", 1)]
public sealed record BeginCriteriaPublication(Uuid EditionId, Uuid AttemptId, string WitnessIdentifier,
    Uuid WitnessIncarnation)
    : IRequest<CriteriaOperationReceipt>, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => AttemptId;
}

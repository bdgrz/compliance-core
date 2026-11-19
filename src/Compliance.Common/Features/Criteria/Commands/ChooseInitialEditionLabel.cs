using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Chooses another label for the same edition after a definitive initial refusal.</summary>
[Discriminator("bdgrz.criteria.edition.initial-label.choose", 1)]
public sealed record ChooseInitialEditionLabel(Uuid EditionId, Uuid OperationId, string Label)
    : IRequest<CriteriaOperationReceipt>, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => OperationId;
}

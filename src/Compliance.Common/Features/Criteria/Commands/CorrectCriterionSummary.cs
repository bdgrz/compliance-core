using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Corrects the original product summary of one criterion entry.</summary>
[Discriminator("bdgrz.criteria.entry.summary.correct", 1)]
public sealed record CorrectCriterionSummary(Uuid EditionId, string Identifier, Uuid OperationId,
    long ExpectedRevision, string Summary)
    : IRequest<CriteriaOperationReceipt>, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => OperationId;
}

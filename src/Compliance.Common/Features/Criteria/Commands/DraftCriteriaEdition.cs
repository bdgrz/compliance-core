using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Starts a client-identified edition and reserves its requested label.</summary>
[Discriminator("bdgrz.criteria.edition.draft", 1)]
public sealed record DraftCriteriaEdition(Uuid EditionId, Uuid OperationId,
    [property: JsonRequired] string Framework, string Label)
    : IRequest<CriteriaOperationReceipt>, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => OperationId;
}

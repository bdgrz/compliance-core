using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;

namespace Bdgrz.Compliance.Features.Evidence.Commands;

/// <summary>Records one independent terminal approval or reasoned rejection of the exact preparation.</summary>
[Discriminator("bdgrz.evidence.redaction.review", 1)]
public sealed record ReviewEvidenceRedaction(Uuid CommandId, Uuid TenantId, Uuid RedactionId,
    long ExpectedRevision, Uuid PreparationId, long PreparedRevision, bool Approved,
    string? Comments = null, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<EvidenceRedactionView>, IEvidenceRedactionMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

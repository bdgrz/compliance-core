using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Evidence.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence.Commands;

/// <summary>A personal approval of exactly one retained preparation; HTTP only.</summary>
[Discriminator("bdgrz.evidence.redaction.approve", 2)]
public sealed record ApproveEvidenceRedaction(Uuid CommandId, Uuid TenantId, Uuid RedactionId, long ExpectedRevision,
    Uuid PreparationId, long PreparedRevision, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<EvidenceRedactionView>, IEvidenceRedactionMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

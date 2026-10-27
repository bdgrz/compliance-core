using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

[Discriminator("bdgrz.evidence.redaction.prepare", 2)]
public sealed record PrepareEvidenceRedaction(Uuid CommandId, Uuid TenantId, Uuid RedactionId, long ExpectedRevision,
    Uuid OriginalArtifactId, Uuid DerivedArtifactId, string Provenance, string Reason)
    : IRequest<EvidenceRedactionView>, IEvidenceRedactionMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

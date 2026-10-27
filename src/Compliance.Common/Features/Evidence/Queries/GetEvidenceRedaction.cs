using Bdgrz.Compliance.Features.Evidence.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence.Queries;

[Discriminator("bdgrz.evidence.redaction.get", 1)]
public sealed record GetEvidenceRedaction(Uuid TenantId, Uuid RedactionId)
    : IRequest<EvidenceRedactionView>, ICallable;

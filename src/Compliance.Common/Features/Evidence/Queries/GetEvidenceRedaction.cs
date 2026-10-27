using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Evidence.Queries;

[Discriminator("bdgrz.evidence.redaction.get", 1)]
public sealed record GetEvidenceRedaction(Uuid TenantId, Uuid RedactionId)
    : IRequest<EvidenceRedactionView>, ICallable;

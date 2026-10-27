using Bdgrz.Compliance.Features.Evidence.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence.Events;

[Discriminator("bdgrz.evidence.redaction.prepared", 1)]
public sealed record EvidenceRedactionPrepared(Uuid TenantId, Uuid RedactionId,
    long ExpectedRevision, EvidenceRedactionPreparationFact Preparation) : DomainEvent;

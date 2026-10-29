using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Evidence.Events;

[Discriminator("bdgrz.evidence.redaction.approved", 1)]
public sealed record EvidenceRedactionApproved(Uuid TenantId, Uuid RedactionId,
    long ExpectedRevision, EvidenceRedactionApprovalFact Approval) : DomainEvent;

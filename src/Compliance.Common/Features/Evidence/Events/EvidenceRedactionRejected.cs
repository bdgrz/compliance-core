using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.Evidence.Events;

[Discriminator("bdgrz.evidence.redaction.rejected", 1)]
public sealed record EvidenceRedactionRejected(Uuid TenantId, Uuid RedactionId, long ExpectedRevision,
    EvidenceRedactionRejectionFact Rejection, OperationProvenance Operation, ImmutableContentRef SourceProofs,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

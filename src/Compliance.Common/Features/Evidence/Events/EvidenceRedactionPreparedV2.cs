using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Evidence.Events;

/// <summary>Native preparation with exact source qualification and frozen original result; occupied inline v1 remains legacy.</summary>
[Discriminator("bdgrz.evidence.redaction.prepared", 2)]
public sealed record EvidenceRedactionPreparedV2(Uuid TenantId, Uuid RedactionId, long ExpectedRevision,
    EvidenceRedactionPreparationFact Preparation, OperationProvenance Operation, ImmutableContentRef SourceProofs,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

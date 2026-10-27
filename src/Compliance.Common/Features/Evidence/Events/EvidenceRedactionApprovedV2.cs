using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Evidence.Events;

/// <summary>Native personal approval of an exact preparation and observed available derivative.</summary>
[Discriminator("bdgrz.evidence.redaction.approved", 2)]
public sealed record EvidenceRedactionApprovedV2(Uuid TenantId, Uuid RedactionId, long ExpectedRevision,
    EvidenceRedactionApprovalFact Approval, OperationProvenance Operation, ImmutableContentRef SourceProofs,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

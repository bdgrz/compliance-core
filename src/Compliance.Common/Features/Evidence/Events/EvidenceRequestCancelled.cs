using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Cancels an open request with an attributed rationale; cancellation is final.</summary>
[Discriminator("bdgrz.evidence.request.cancelled", 2)]
public sealed record EvidenceRequestCancelled(Uuid TenantId, Uuid ProgramId, Uuid EvidenceRequestId,
    long Revision, string Rationale, ActorReference CancelledBy, DateTimeOffset CancelledAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

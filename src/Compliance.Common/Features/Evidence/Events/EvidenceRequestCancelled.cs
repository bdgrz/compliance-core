using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Evidence.Events;

/// <summary>Cancels an open request with an attributed rationale; cancellation is final.</summary>
[Discriminator("bdgrz.evidence.request.cancelled", 2)]
public sealed record EvidenceRequestCancelled(Uuid TenantId, Uuid ProgramId, Uuid EvidenceRequestId,
    long Revision, string Rationale, ActorReference CancelledBy, DateTimeOffset CancelledAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

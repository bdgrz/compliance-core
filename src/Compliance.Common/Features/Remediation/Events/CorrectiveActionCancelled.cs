using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>Open corrective work was cancelled with its reason, actor and time preserved.</summary>
[Discriminator("bdgrz.finding.corrective_action.cancelled", 1)]
public sealed record CorrectiveActionCancelled(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, Uuid ActionId, string Reason, Uuid CancelledByMemberId, ActorReference CancelledBy,
    DateTimeOffset CancelledAt, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

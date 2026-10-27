using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Events;

/// <summary>Unfinished treatment work was canceled while its history is retained.</summary>
[Discriminator("bdgrz.risk.treatment_action.cancelled", 2)]
public sealed record RiskTreatmentActionCancelled(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision,
    Uuid ActionId, Uuid CancellationId, string Rationale, ActorReference CancelledBy, DateTimeOffset CancelledAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

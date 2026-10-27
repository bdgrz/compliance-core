using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Events;

/// <summary>An independent reviewer accepted or rejected the pending control treatment assertion of the line.</summary>
[Discriminator("bdgrz.risk.control_treatment.reviewed", 2)]
public sealed record RiskControlTreatmentReviewed(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision,
    Uuid TreatmentId, Uuid DecisionId, string Outcome, string Rationale, ActorReference Reviewer,
    DateTimeOffset ReviewedAt, Uuid? SeparationOfDutiesWaiverId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

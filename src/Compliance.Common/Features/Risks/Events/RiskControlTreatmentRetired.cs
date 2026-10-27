using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Events;

/// <summary>An accepted control treatment assertion was withdrawn; its history is kept.</summary>
[Discriminator("bdgrz.risk.control_treatment.retired", 2)]
public sealed record RiskControlTreatmentRetired(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision,
    Uuid TreatmentId, string Rationale, ActorReference RetiredBy, DateTimeOffset RetiredAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

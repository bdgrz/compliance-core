using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     A durable reassessment trigger was raised for the risk. <paramref name="TriggerContent" /> is the complete
///     <see cref="RiskReassessmentTriggerView" />.
/// </summary>
[Discriminator("bdgrz.risk.reassessment.triggered", 2)]
public sealed record RiskReassessmentTriggered(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision,
    ImmutableContentRef TriggerContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

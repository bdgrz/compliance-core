using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>One complete immutable plan version accepted on its control's plan line.</summary>
[Discriminator("bdgrz.control.evaluation.plan.version-defined", 2)]
public sealed record ControlEvaluationPlanVersionDefined(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid ControlVersionId, Uuid PlanVersionId, long Version, ImmutableContentRef Content,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

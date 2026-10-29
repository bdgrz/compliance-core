using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Evaluations.Events;

/// <summary>One complete immutable plan version accepted on its control's plan line.</summary>
[Discriminator("bdgrz.control.evaluation.plan.version-defined", 2)]
public sealed record ControlEvaluationPlanVersionDefined(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid ControlVersionId, Uuid PlanVersionId, long Version, ImmutableContentRef Content,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

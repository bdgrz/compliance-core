using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Evaluations.Events;

/// <summary>
///     A step result of one evaluation. The result with its complete inspected items and the deviation it raised, if
///     any, are verified immutable content.
/// </summary>
[Discriminator("bdgrz.control.evaluation.step_recorded", 2)]
public sealed record ControlEvaluationStepRecorded(Uuid TenantId, Uuid ProgramId, Uuid ControlId, Uuid EvaluationId,
    long Revision, Uuid StepId, ImmutableContentRef ResultContent, ImmutableContentRef? DeviationContent,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

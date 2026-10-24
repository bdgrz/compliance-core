using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>The disposition of one minor deviation of one evaluation.</summary>
[Discriminator("bdgrz.control.evaluation.deviation_disposed", 2)]
public sealed record ControlEvaluationDeviationDisposed(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid EvaluationId, long Revision, Uuid DeviationId, string Disposition, string Rationale, Uuid? WaiverId,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Evaluations.Events;

/// <summary>The disposition of one minor deviation of one evaluation.</summary>
[Discriminator("bdgrz.control.evaluation.deviation_disposed", 2)]
public sealed record ControlEvaluationDeviationDisposed(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid EvaluationId, long Revision, Uuid DeviationId, string Disposition, string Rationale, Uuid? WaiverId,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

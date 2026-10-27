using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Evaluations.Events;

/// <summary>
///     One submitted round of one evaluation. The complete submission, with the exact step results and deviations it
///     rests on, is verified immutable content.
/// </summary>
[Discriminator("bdgrz.control.evaluation.submitted", 2)]
public sealed record ControlEvaluationSubmitted(Uuid TenantId, Uuid ProgramId, Uuid ControlId, Uuid EvaluationId,
    long Revision, ImmutableContentRef SubmissionContent, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

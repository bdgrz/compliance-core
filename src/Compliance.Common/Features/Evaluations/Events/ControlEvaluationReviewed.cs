using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Evaluations.Events;

/// <summary>
///     The independent review decision on the submitted round. <paramref name="Decision" /> (accepted, rejected or
///     changes_requested) decides the lifecycle of the evaluation; the complete decision is verified immutable content.
/// </summary>
[Discriminator("bdgrz.control.evaluation.reviewed", 2)]
public sealed record ControlEvaluationReviewed(Uuid TenantId, Uuid ProgramId, Uuid ControlId, Uuid EvaluationId,
    long Revision, string Decision, ImmutableContentRef ReviewContent, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;

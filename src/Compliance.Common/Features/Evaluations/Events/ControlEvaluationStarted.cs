using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Evaluations.Events;

/// <summary>
///     The start of one evaluation, which is its own owner stream. The frozen procedure and the retested deviation ids
///     are verified immutable content, so nothing is truncated. A retest names the accepted review fact of its
///     predecessor in <paramref name="RetestPredecessor" />; the predecessor itself is never written.
/// </summary>
[Discriminator("bdgrz.control.evaluation.started", 2)]
public sealed record ControlEvaluationStarted(Uuid TenantId, Uuid ProgramId, Uuid ControlId, Uuid EvaluationId,
    long Revision, Uuid ControlVersionId, Uuid PlanVersionId, long PlanVersion, ImmutableContentRef StepsManifest,
    Uuid EvaluatorMemberId, ActorReference StartedBy, DateTimeOffset StartedAt, Uuid? RetestOfEvaluationId,
    NativeFactReference? RetestPredecessor, ImmutableContentRef RetestOfDeviationIdsManifest,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;

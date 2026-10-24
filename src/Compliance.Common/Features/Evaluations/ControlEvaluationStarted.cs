using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

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

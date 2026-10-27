using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     The practice subject pair of the generic settle receiver. The terminal fact is resolved from the engagement
///     owner; a completed operation requires its exact permanent commitment, a rejected one fences the key so a
///     delayed reservation is refused. A settlement never removes a permanent commitment.
/// </summary>
sealed class SettlePracticeIndependenceReservationHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementAcceptanceVerifier verifier)
    : SettleSourceUseHandler<PracticeIndependenceCommitment, SettlePracticeIndependenceReservation>(reader, executor,
        content, clock)
{
    protected override PracticeIndependenceCommitment Open(SettlePracticeIndependenceReservation request,
        Uuid selectedCommandId) =>
        new(EngagementPersonalDecisionVerifier.TenantOf(request.Target), request.SubjectKind, request.SubjectId,
            selectedCommandId);

    protected override ValueTask<Result<SourceUseSource>> VerifyAsync(SettlePracticeIndependenceReservation request,
        CancellationToken ct) => verifier.SettleAsync(request.Source, request.Intent, request.OperationId, Uuid.Empty,
        request.CommandId, request.Target, request.Generation, nameof(SettlePracticeIndependenceReservation), ct);

    protected override Func<PracticeIndependenceCommitment, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation() =>
        (PracticeIndependenceCommitment owner, OperationProvenance provenance, ImmutableContentRef intent,
            long generation) => owner.SettleReservation(provenance, intent, generation);
}

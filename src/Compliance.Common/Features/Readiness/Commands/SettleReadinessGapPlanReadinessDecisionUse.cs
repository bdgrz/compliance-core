using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Settles the readiness decision use of one gap plan. <paramref name="Source" /> is the terminal program fact: the
///     recorded decision for <see cref="SourceUseSettlementDisposition.IrreversibleDecision" />, the cancellation for
///     <see cref="SourceUseSettlementDisposition.Canceled" />. A settlement before any grant fences the key so a delayed
///     acquisition cannot recreate the authority.
/// </summary>
[Discriminator("bdgrz.readiness-gap-plan.settle-readiness-decision-use", 1)]
public sealed record SettleReadinessGapPlanReadinessDecisionUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid ReceivingOwnerId, SourceUseSettlementDisposition Disposition)
    : ISourceUseSettlement, IReadinessReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Readiness.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.Readiness.Commands;

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

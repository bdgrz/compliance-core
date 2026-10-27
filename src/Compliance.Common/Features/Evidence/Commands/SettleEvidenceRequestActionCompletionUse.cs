using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     Settles the action completion review use of one evidence request. <paramref name="Source" /> is the terminal
///     action fact: the review for <see cref="SourceUseSettlementDisposition.IrreversibleDecision" />, the cancellation
///     for <see cref="SourceUseSettlementDisposition.Canceled" />. A settlement before any grant fences the key so a
///     delayed acquisition cannot recreate the authority.
/// </summary>
[Discriminator("bdgrz.evidence-request.settle-action-completion-use", 1)]
public sealed record SettleEvidenceRequestActionCompletionUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid ReceivingOwnerId, Uuid ProgramId, SourceUseSettlementDisposition Disposition)
    : ISourceUseSettlement, IEvidenceRequestReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

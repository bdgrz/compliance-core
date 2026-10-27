using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.Evidence.Commands;

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

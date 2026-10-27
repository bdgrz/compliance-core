using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>
///     Settles the closure use of one corrective action. <paramref name="Source" /> is the terminal finding fact: the
///     closure for <see cref="SourceUseSettlementDisposition.ParentClosed" />, the cancellation for
///     <see cref="SourceUseSettlementDisposition.Canceled" />. A settlement before any grant fences the key so a delayed
///     acquisition cannot recreate the authority.
/// </summary>
[Discriminator("bdgrz.corrective-action.settle-finding-closure-use", 1)]
public sealed record SettleCorrectiveActionFindingClosureUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid ReceivingOwnerId, Uuid ProgramId, SourceUseSettlementDisposition Disposition)
    : ISourceUseSettlement, IFindingReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

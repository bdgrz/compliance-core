using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditorRequests.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Commands;

/// <summary>
///     Settles the resolution use of one response. <paramref name="Source" /> is the terminal request fact: the
///     resolution for <see cref="SourceUseSettlementDisposition.ParentClosed" />, the cancellation for
///     <see cref="SourceUseSettlementDisposition.Canceled" />. A settlement before any grant fences the key so a delayed
///     acquisition cannot recreate the authority.
/// </summary>
[Discriminator("bdgrz.auditor-response.settle-request-resolution-use", 1)]
public sealed record SettleAuditorResponseRequestResolutionUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid ReceivingOwnerId, Uuid ProgramId, SourceUseSettlementDisposition Disposition)
    : ISourceUseSettlement, IAuditorRequestReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

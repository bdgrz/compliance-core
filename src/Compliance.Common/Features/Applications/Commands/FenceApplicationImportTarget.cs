using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Records the batch's fence on a target even when its hold was never acquired. Every later reserve or apply of
///     that batch is refused locally, so a delayed operation never meets a released hold.
/// </summary>
[Discriminator("bdgrz.declared_application.fence_import_target", 1)]
public sealed record FenceApplicationImportTarget(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid BatchId, Uuid ApplicationId)
    : IRequest<ReceiverOutcome>, IApplicationImportReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

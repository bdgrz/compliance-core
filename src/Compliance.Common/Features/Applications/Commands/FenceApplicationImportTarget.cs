using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Commands;

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

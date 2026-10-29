using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
namespace Bdgrz.Compliance.Features.Applications.Commands;

/// <summary>
///     Completes an accepted cancellation once the manifest proves every target is fenced and the batch has not
///     published; otherwise the batch recovers forward.
/// </summary>
[Discriminator("bdgrz.application_import.complete_cancellation", 1)]
public sealed record CompleteApplicationImportCancellation(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid BatchId, ImmutableContentRef TargetFenceManifest)
    : IRequest<ReceiverOutcome>, IApplicationImportReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

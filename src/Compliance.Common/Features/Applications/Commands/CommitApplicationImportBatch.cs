using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Commands;

/// <summary>
///     The one publication gate of a batch: it publishes every target together once the manifest carries a native
///     proof for every frozen row and the batch is not canceled.
/// </summary>
[Discriminator("bdgrz.application_import.commit", 1)]
public sealed record CommitApplicationImportBatch(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid BatchId, ImmutableContentRef TargetProofManifest)
    : IRequest<ReceiverOutcome>, IApplicationImportReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

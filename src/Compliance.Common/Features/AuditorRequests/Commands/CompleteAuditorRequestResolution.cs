using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Commands;

/// <summary>
///     Appends <see cref="AuditorRequestResolved" /> once every admitted child is materialized and every response is held
///     under this resolution's grant. <paramref name="ResponseProofManifest" /> is the complete manifest of the held
///     accepted dispositions; the receiver re-derives it from the authoritative responses and refuses any difference.
///     A resolution canceled or superseded by a reopened generation is a terminal no-op.
/// </summary>
[Discriminator("bdgrz.auditor-request.complete-resolution", 1)]
public sealed record CompleteAuditorRequestResolution(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid RequestId, ImmutableContentRef ResponseProofManifest)
    : IRequest<ReceiverOutcome>, IAuditorRequestReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

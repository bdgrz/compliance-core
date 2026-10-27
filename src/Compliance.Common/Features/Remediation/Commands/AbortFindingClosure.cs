using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>
///     Cancels the exact pending closure when a permanent proof mismatch makes it impossible and no final decision was
///     made. The cancellation fences a delayed <see cref="CompleteFindingClosure" /> atomically; a closure that already
///     closed the finding recovers forward and is not canceled.
/// </summary>
[Discriminator("bdgrz.finding.abort-closure", 1)]
public sealed record AbortFindingClosure(Uuid CommandId, Uuid OperationId, EventStreamAddress Target,
    long Generation, long ExpectedRevision, ImmutableContentRef FailureProof)
    : IRequest<ReceiverOutcome>, IFindingReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

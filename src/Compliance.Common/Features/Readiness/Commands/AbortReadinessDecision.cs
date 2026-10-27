using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Commands;

/// <summary>
///     Cancels the exact pending decision when an authoritative permanent proof mismatch makes it impossible and no
///     final decision was made. The cancellation fences a delayed completion atomically; a decision that was already
///     recorded recovers forward and is not canceled. There is no public actor or intent replacement.
/// </summary>
[Discriminator("bdgrz.program-readiness.abort-readiness-decision", 1)]
public sealed record AbortReadinessDecision(Uuid CommandId, Uuid OperationId, EventStreamAddress Target,
    long Generation, long ExpectedRevision, ImmutableContentRef FailureProof)
    : IRequest<ReceiverOutcome>, IReadinessReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

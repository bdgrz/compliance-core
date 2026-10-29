using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditorRequests.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Commands;

/// <summary>
///     Cancels the exact pending resolution when a permanent proof mismatch makes it impossible and no final decision
///     was made. The cancellation fences a delayed <see cref="CompleteAuditorRequestResolution" /> atomically; a
///     resolution that already resolved the request recovers forward and is not canceled.
/// </summary>
[Discriminator("bdgrz.auditor-request.abort-resolution", 1)]
public sealed record AbortAuditorRequestResolution(Uuid CommandId, Uuid OperationId, EventStreamAddress Target,
    long Generation, long ExpectedRevision, ImmutableContentRef FailureProof)
    : IRequest<ReceiverOutcome>, IAuditorRequestReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

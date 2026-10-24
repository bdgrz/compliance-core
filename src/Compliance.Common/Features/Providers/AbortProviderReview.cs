using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Cancels the exact pending review after an authoritative permanent proof mismatch, before the irreversible
///     recording. A recorded review recovers forward; there is no public actor or intent replacement.
/// </summary>
[Discriminator("bdgrz.provider-assurance-review.abort-provider-review", 1)]
public sealed record AbortProviderReview(Uuid CommandId, EventStreamAddress Target, Uuid OperationId, long Generation,
    long? ExpectedRevision, ImmutableContentRef FailureProof) : IRequest<ReceiverOutcome>, IIdentifiedRequest,
    IProviderAssuranceReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

using Cntryl.Portia;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Providers.Commands;

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

using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Cancels the exact pending closure when an authoritative permanent proof mismatch shows it can never complete.
///     The cancellation fences every delayed local finalization of the generation atomically. A closure that already
///     completed recovers forward and is never aborted. <paramref name="FailureProof" /> is the frozen closure intent.
/// </summary>
[Discriminator("bdgrz.policy-distribution-campaign.abort-policy-campaign-closure", 1)]
public sealed record AbortPolicyCampaignClosure(Uuid CommandId, Uuid OperationId, EventStreamAddress Target,
    long Generation, long ExpectedRevision, ImmutableContentRef FailureProof)
    : IRequest<ReceiverOutcome>, IPolicyCampaignReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

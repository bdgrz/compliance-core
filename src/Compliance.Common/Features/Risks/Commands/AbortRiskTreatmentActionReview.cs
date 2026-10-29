using Cntryl.Portia;
using Bdgrz.Compliance.Features.Risks.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Risks.Commands;

/// <summary>
///     Cancels the exact pending completion review when a permanent proof mismatch makes it impossible and no final
///     decision was made. The cancellation fences a delayed <see cref="CompleteRiskTreatmentActionReview" /> atomically;
///     a review that already recorded its decision recovers forward and is not canceled.
/// </summary>
[Discriminator("bdgrz.risk-treatment-action.abort-review", 1)]
public sealed record AbortRiskTreatmentActionReview(Uuid CommandId, Uuid OperationId, EventStreamAddress Target,
    long Generation, long ExpectedRevision, ImmutableContentRef FailureProof)
    : IRequest<ReceiverOutcome>, IRiskActionReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

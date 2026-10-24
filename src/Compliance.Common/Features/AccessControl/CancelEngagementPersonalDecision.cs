using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Cancels an admitted personal decision before authorization, fencing authorization against late delivery. <see cref="Source" /> is the accepted admission. An authorized decision recovers forward and is never canceled.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.cancel-personal-decision", 1)]
public sealed record CancelEngagementPersonalDecision(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid EngagementId)
    : IRequest<ReceiverOutcome>, IEngagementPersonalDecisionReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

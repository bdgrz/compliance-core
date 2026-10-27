using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

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

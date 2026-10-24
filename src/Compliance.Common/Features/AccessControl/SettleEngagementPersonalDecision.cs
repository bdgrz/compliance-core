using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Releases the engagement content pin of a materialized personal decision and records the exact decision pointer. <see cref="Source" /> is the materialized child fact.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.settle-personal-decision", 1)]
public sealed record SettleEngagementPersonalDecision(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid EngagementId)
    : IRequest<ReceiverOutcome>, IEngagementPersonalDecisionReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The irreversible authorization of a personal decision, taken only when every required source use is held. It freezes the decision instant. <see cref="SourceUseProofs" /> lists the exact grant facts of the acquire stage.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.authorize-personal-decision", 1)]
public sealed record AuthorizeEngagementPersonalDecision(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid EngagementId, ImmutableContentRef SourceUseProofs)
    : IRequest<ReceiverOutcome>, IEngagementPersonalDecisionReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

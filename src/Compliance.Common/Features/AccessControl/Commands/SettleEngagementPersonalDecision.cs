using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

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

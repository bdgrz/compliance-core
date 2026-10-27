using Cntryl.Portia;
using Bdgrz.Compliance.Features.Readiness.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Readiness.Commands;

/// <summary>
///     Appends <see cref="ReadinessDecisionRecorded" /> once every gap plan is held. <paramref name="PlanProofs" /> is the
///     complete manifest of the held plan facts; the receiver re-derives it from the authoritative gap plans and refuses
///     any difference. A decision canceled by a fence is a terminal no-op.
/// </summary>
[Discriminator("bdgrz.program-readiness.complete-readiness-decision", 1)]
public sealed record CompleteReadinessDecision(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid ProgramId,
    Uuid AssessmentId, ImmutableContentRef PlanProofs)
    : IRequest<ReceiverOutcome>, IReadinessReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

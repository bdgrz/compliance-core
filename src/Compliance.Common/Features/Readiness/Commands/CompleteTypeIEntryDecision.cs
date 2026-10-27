using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Commands;

/// <summary>
///     Appends <see cref="TypeIEntryDecisionRecorded" /> once every gap plan is held. The receiver re-derives the held
///     plan proofs, checks that the complete acknowledgement set, the proceed prerequisite and the single approved-entry
///     slot still hold at the decision instant, and refuses any difference.
/// </summary>
[Discriminator("bdgrz.program-readiness.complete-type-i-entry-decision", 1)]
public sealed record CompleteTypeIEntryDecision(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid ProgramId,
    Uuid AssessmentId, ImmutableContentRef PlanProofs)
    : IRequest<ReceiverOutcome>, IReadinessReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;

/// <summary>
///     Materializes one admitted feedback contribution on its own thread owner at revision one, also after the handoff
///     was revoked or closed. <paramref name="Source" /> is the <see cref="AdvisorFeedbackAdmitted" /> fact on the
///     handoff, <paramref name="Intent" /> its frozen <c>ChildAdmissionIntent</c> and <paramref name="Generation" /> the
///     selection generation at admission. The receiver verifies the source on the authoritative handoff before it
///     appends anything and never loads the live handoff to replay the contribution.
/// </summary>
[Discriminator("bdgrz.advisor-feedback.materialize", 1)]
public sealed record MaterializeAdvisorFeedback(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid HandoffId, Uuid FeedbackId)
    : IRequest<ReceiverOutcome>, IAdvisorReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

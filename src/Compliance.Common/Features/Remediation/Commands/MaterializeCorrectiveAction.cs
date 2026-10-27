using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>
///     Materializes one admitted corrective action on its own owner at revision zero. <paramref name="Source" /> is the
///     <see cref="CorrectiveActionAdmitted" /> fact on the finding, <paramref name="Intent" /> its frozen
///     <c>ChildAdmissionIntent</c> and <paramref name="Generation" /> the finding generation at admission. The receiver
///     verifies the source on the authoritative finding before it appends anything.
/// </summary>
[Discriminator("bdgrz.corrective-action.materialize", 1)]
public sealed record MaterializeCorrectiveAction(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid FindingId, Uuid ActionId)
    : IRequest<ReceiverOutcome>, IFindingReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Rejects a pending positive staff assignment before the irreversible authorization, fencing it against late
///     delivery. The accepted engagement and every earlier permanent wall are retained; an authorized assignment
///     recovers forward and is never rejected.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.reject-service-engagement-staff-assignment", 1)]
public sealed record RejectServiceEngagementStaffAssignment(Uuid CommandId, Uuid OperationId,
    EventStreamAddress Target, long Generation, long ExpectedRevision, ImmutableContentRef FailureProof)
    : IRequest<ReceiverOutcome>, IEngagementAcceptanceReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

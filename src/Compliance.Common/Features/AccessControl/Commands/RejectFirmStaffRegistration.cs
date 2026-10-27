using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Rejects a pending registration whose canonical user is permanently claimed by a different staff record. A
///     registration that already completed recovers forward and is not rejected.
/// </summary>
[Discriminator("bdgrz.firm-staff-record.reject-registration", 1)]
public sealed record RejectFirmStaffRegistration(Uuid CommandId, Uuid OperationId, EventStreamAddress Target,
    long Generation, long ExpectedRevision, ImmutableContentRef FailureProof)
    : IRequest<ReceiverOutcome>, IFirmStaffRegistrationReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

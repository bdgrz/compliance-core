using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Completes a pending staff registration once the exact permanent claim exists.</summary>
[Discriminator("bdgrz.firm-staff-record.complete-registration", 1)]
public sealed record CompleteFirmStaffRegistration(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid StaffMemberId, Uuid UserId) : IRequest<ReceiverOutcome>, IFirmStaffRegistrationReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

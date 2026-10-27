using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>Completes a pending staff registration once the exact permanent claim exists.</summary>
[Discriminator("bdgrz.firm-staff-record.complete-registration", 1)]
public sealed record CompleteFirmStaffRegistration(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid StaffMemberId, Uuid UserId) : IRequest<ReceiverOutcome>, IFirmStaffRegistrationReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

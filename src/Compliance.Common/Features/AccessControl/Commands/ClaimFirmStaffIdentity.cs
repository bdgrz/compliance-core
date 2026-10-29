using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Permanently binds a canonical user to the staff record whose registration was accepted. A claim owned by a
///     different staff record rejects the pending registration instead.
/// </summary>
[Discriminator("bdgrz.firm-staff-identity-claim.claim", 1)]
public sealed record ClaimFirmStaffIdentity(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid StaffMemberId,
    Uuid UserId, string Practice) : IRequest<ReceiverOutcome>, IFirmStaffRegistrationReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

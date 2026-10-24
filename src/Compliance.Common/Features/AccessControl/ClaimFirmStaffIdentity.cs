using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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

using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Records or revokes the designation once the assertion claim exists. Matches the accepted change and the
///     monotonic predecessor signed authority revision; signed validity is checked at the frozen decision instant.
/// </summary>
[Discriminator("bdgrz.firm-professional-duty-designation.apply-change", 1)]
public sealed record ApplyProfessionalDutyChange(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid DesignationId, Uuid AssertionId, NativeFactReference? StaffEligibilityUse)
    : IRequest<ReceiverOutcome>, IProfessionalDutyChangeReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}

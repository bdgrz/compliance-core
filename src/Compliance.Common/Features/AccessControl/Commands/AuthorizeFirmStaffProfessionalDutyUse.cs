using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Acquires the exact currently active canonical staff record for recording a designation. The staff record
///     refuses status changes while the use is held. <see cref="Source" /> is the accepted change fact; the receiver
///     verifies it by hydrating the designation owner.
/// </summary>
[Discriminator("bdgrz.firm-staff-record.authorize-professional-duty-use", 1)]
public sealed record AuthorizeFirmStaffProfessionalDutyUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid SourceContentEventId, ulong ExpectedContentRevision, Uuid DesignationId)
    : ISourceUseAuthorization, IProfessionalDutyChangeReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
